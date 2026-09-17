using System.Net;
using System.Text;
using ThreatIntelligence.Application.Abstractions.Secrets;
using ThreatIntelligence.Application.AI;
using ThreatIntelligence.Infrastructure.AI;
using Xunit;

namespace ThreatIntelligence.UnitTests.AI;

public sealed class OpenAiCompatibleChatClientTests
{
    private static readonly ChatCompletionRequest Request = new(
        "http://modelserver:11434",
        "llama3.1",
        [new ChatCompletionMessage("system", "policy"), new ChatCompletionMessage("user", "evidence")],
        0.2,
        512);

    private static OpenAiCompatibleChatClient CreateClient(
        FakeUpstreamHandler handler,
        string? storedKey = null)
    {
        var httpClient = new HttpClient(handler);
        var secretStore = new StubSecretStore(storedKey);
        return new OpenAiCompatibleChatClient(httpClient, secretStore, AiClientOptions.Default, new StubLogger());
    }

    [Fact]
    public async Task Keyless_server_gets_no_authorization_header_and_streams_deltas()
    {
        var handler = new FakeUpstreamHandler(Sse(
            """{"choices":[{"delta":{"content":"Rapor"}}]}""",
            """{"choices":[{"delta":{"content":" basarili"}}]}""",
            """{"choices":[{"delta":{},"finish_reason":"stop"}]}""",
            "[DONE]"));
        var client = CreateClient(handler);

        var deltas = await CollectAsync(client.StreamAsync(Request, CancellationToken.None));

        Assert.Equal(["Rapor", " basarili"], deltas);
        Assert.Null(handler.AuthorizationHeader);
        Assert.Equal("http://modelserver:11434/chat/completions", handler.RequestUri!.ToString());
    }

    [Fact]
    public async Task Configured_key_is_sent_as_bearer_header()
    {
        var handler = new FakeUpstreamHandler(Sse("""{"choices":[{"delta":{"content":"ok"}}]}""", "[DONE]"));
        var client = CreateClient(handler, storedKey: "test-secret-key");

        await CollectAsync(client.StreamAsync(Request, CancellationToken.None));

        Assert.Equal("Bearer test-secret-key", handler.AuthorizationHeader);
    }

    [Fact]
    public async Task Upstream_error_maps_to_upstream_failure_with_status()
    {
        var handler = new FakeUpstreamHandler(HttpStatusCode.InternalServerError, "boom");
        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<AiUpstreamException>(
            async () => await CollectAsync(client.StreamAsync(Request, CancellationToken.None)));

        Assert.Equal(AiErrorCodes.UpstreamFailure, exception.Code);
        Assert.Equal(500, exception.UpstreamStatus);
    }

    [Fact]
    public async Task Malformed_upstream_line_fails_with_unsupported_response()
    {
        var handler = new FakeUpstreamHandler("this is not an sse frame\n");
        var client = CreateClient(handler);

        var exception = await Record.ExceptionAsync(
            async () => await CollectAsync(client.StreamAsync(Request, CancellationToken.None)));

        Assert.IsType<AiUpstreamException>(exception);
        Assert.Equal(AiErrorCodes.UnsupportedUpstreamResponse, ((AiUpstreamException)exception).Code);
    }

    [Fact]
    public async Task Malformed_json_chunk_fails_with_unsupported_response()
    {
        var handler = new FakeUpstreamHandler(Sse("{not json}", "[DONE]"));
        var client = CreateClient(handler);

        var exception = await Record.ExceptionAsync(
            async () => await CollectAsync(client.StreamAsync(Request, CancellationToken.None)));

        Assert.IsType<AiUpstreamException>(exception);
        Assert.Equal(AiErrorCodes.UnsupportedUpstreamResponse, ((AiUpstreamException)exception).Code);
    }

    [Fact]
    public async Task Request_cancellation_stops_the_stream_without_later_output()
    {
        var handler = new FakeUpstreamHandler(Sse(
            """{"choices":[{"delta":{"content":"one"}}]}""",
            """{"choices":[{"delta":{"content":"two"}}]}""",
            "[DONE]"), delayAfter: 1);
        var client = CreateClient(handler);

        using var cts = new CancellationTokenSource();
        var deltas = new List<string>();
        OperationCanceledException? cancellation = null;
        try
        {
            await foreach (var delta in client.StreamAsync(Request, cts.Token))
            {
                deltas.Add(delta.Text);
                cts.Cancel();
            }
        }
        catch (OperationCanceledException exception)
        {
            cancellation = exception;
        }

        Assert.Single(deltas);
        Assert.Equal("one", deltas[0]);
        Assert.NotNull(cancellation);
    }

    [Fact]
    public async Task Split_sse_data_lines_are_joined_into_one_frame()
    {
        var handler = new FakeUpstreamHandler(
            "data: {\"choices\":[{\"delta\":{\"cont\n" +
            "ent\":\"split\"}}]}\n" +
            "\n" +
            "data: [DONE]\n" +
            "\n");
        var client = CreateClient(handler);

        var deltas = await CollectAsync(client.StreamAsync(Request, CancellationToken.None));

        Assert.Equal(["split"], deltas);
    }

    private static async Task<IReadOnlyList<string>> CollectAsync(
        IAsyncEnumerable<ChatCompletionDelta> stream)
    {
        var values = new List<string>();
        await foreach (var delta in stream)
        {
            values.Add(delta.Text);
        }

        return values;
    }

    private static string Sse(params string[] dataLines)
    {
        var builder = new StringBuilder();
        foreach (var data in dataLines)
        {
            builder.Append("data: ").Append(data).Append("\n\n");
        }

        return builder.ToString();
    }

    private sealed class FakeUpstreamHandler(HttpStatusCode status, string body, int delayAfter = int.MaxValue)
        : HttpMessageHandler
    {
        public FakeUpstreamHandler(string body, int delayAfter = int.MaxValue) : this(HttpStatusCode.OK, body, delayAfter) { }

        public string? AuthorizationHeader { get; private set; }
        public Uri? RequestUri { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            AuthorizationHeader = request.Headers.Authorization?.ToString();
            RequestUri = request.RequestUri;

            if (status != HttpStatusCode.OK)
            {
                return new HttpResponseMessage(status)
                {
                    Content = new StringContent(body, Encoding.UTF8, "text/plain"),
                };
            }

            var lines = body.Split('\n');
            var stream = new MemoryStream();
            for (var index = 0; index < lines.Length; index++)
            {
                var bytes = Encoding.UTF8.GetBytes(lines[index] + "\n");
                await stream.WriteAsync(bytes, cancellationToken);
                stream.Flush();
                if (index >= delayAfter)
                {
                    // Simulate a slow upstream frame so cancellation can land between lines.
                    try
                    {
                        await Task.Delay(Timeout.Infinite, cancellationToken);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        await stream.DisposeAsync();
                        return new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new ByteArrayContent(stream.ToArray())
                            {
                                Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/event-stream") }
                            }
                        };
                    }
                }
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(stream.ToArray())
                {
                    Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/event-stream") }
                }
            };
        }
    }

    private sealed class StubSecretStore(string? value) : ISecretStore
    {
        public Task<ManagedSecretStatus?> GetStatusAsync(string name, CancellationToken cancellationToken) =>
            Task.FromResult<ManagedSecretStatus?>(value is null ? null : new ManagedSecretStatus(name, true, 1, null));

        public Task<string?> ResolveAsync(string name, CancellationToken cancellationToken) =>
            Task.FromResult(value);

        public Task<SecretMutationResult> SetAsync(string name, string value, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> ClearAsync(string name, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class StubLogger : Microsoft.Extensions.Logging.ILogger<OpenAiCompatibleChatClient>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => false;

        public void Log<TState>(
            Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId,
            TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) { }
    }
}
