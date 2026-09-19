using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using ThreatIntelligence.Application.Abstractions.Secrets;
using ThreatIntelligence.Application.AI;
using ThreatIntelligence.Infrastructure.AI;

namespace ThreatIntelligence.UnitTests.AI;

public sealed class OpenAiCompatibleIocChatClientTests
{
    private static readonly IocChatCompletionRequest Request = new(
        "http://modelserver:11434",
        "ioc-model",
        [new IocChatMessage("system", "policy")],
        [new IocChatToolDefinition("ioc_get_detail", "detail", new JsonObject { ["type"] = "object" })],
        1200);

    [Fact]
    public async Task Sends_chat_secret_and_tool_definitions_and_parses_tool_call()
    {
        var handler = new JsonHandler("""
            {"choices":[{"message":{"content":null,"tool_calls":[{"id":"call-1","type":"function","function":{"name":"ioc_get_detail","arguments":"{\"id\":7}"}}]}}]}
            """, HttpStatusCode.OK);
        var client = Create(handler, "chat-secret");

        var response = await client.CompleteAsync(Request, CancellationToken.None);

        Assert.True(response.ToolCallsSupported);
        Assert.Single(response.ToolCalls);
        Assert.Equal("ioc_get_detail", response.ToolCalls[0].Name);
        Assert.Equal("Bearer chat-secret", handler.Authorization);
        Assert.Contains("ioc_get_detail", handler.Body, StringComparison.Ordinal);
        Assert.Contains("ioc-model", handler.Body, StringComparison.Ordinal);
        Assert.Contains("\"tool_choice\":\"auto\"", handler.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Parses_final_text_without_tool_calls()
    {
        var handler = new JsonHandler("""
            {"choices":[{"message":{"content":"Fact: IOC #7 was observed. Evidence: #7"}}]}
            """, HttpStatusCode.OK);

        var response = await Create(handler, "secret").CompleteAsync(Request, CancellationToken.None);

        Assert.Equal("Fact: IOC #7 was observed. Evidence: #7", response.Text);
        Assert.Empty(response.ToolCalls);
        Assert.False(response.ToolCallsSupported);
    }

    [Fact]
    public async Task Marks_tool_support_when_upstream_returns_an_empty_tool_calls_array()
    {
        var handler = new JsonHandler("""
            {"choices":[{"message":{"content":"Fact: IOC #7. Evidence: #7","tool_calls":[]}}]}
            """, HttpStatusCode.OK);

        var response = await Create(handler, "secret").CompleteAsync(Request, CancellationToken.None);

        Assert.True(response.ToolCallsSupported);
        Assert.Empty(response.ToolCalls);
    }

    [Fact]
    public async Task Correlates_tool_result_messages_with_the_tool_call_id()
    {
        var handler = new JsonHandler(
            "{\"choices\":[{\"message\":{\"content\":\"Fact: IOC #7. Evidence: #7\"}}]}",
            HttpStatusCode.OK);
        var request = Request with
        {
            Messages =
            [
                new IocChatMessage("system", "policy"),
                new IocChatMessage("assistant", string.Empty,
                    [new IocChatToolCall("call-1", "ioc_get_detail", "{\\\"id\\\":7}")]),
                new IocChatMessage("tool", "{\\\"id\\\":7}", null, "call-1")
            ]
        };

        await Create(handler, "secret").CompleteAsync(request, CancellationToken.None);

        Assert.Contains("\"tool_call_id\":\"call-1\"", handler.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Rejects_malformed_tool_call_payload()
    {
        var handler = new JsonHandler("""
            {"choices":[{"message":{"content":null,"tool_calls":[{"id":"","type":"function","function":{"name":"","arguments":"{}"}}]}}]}
            """, HttpStatusCode.OK);

        var exception = await Assert.ThrowsAsync<AiUpstreamException>(() =>
            Create(handler, "secret").CompleteAsync(Request, CancellationToken.None));

        Assert.Equal(AiErrorCodes.UnsupportedUpstreamResponse, exception.Code);
    }

    [Fact]
    public async Task Rejects_model_that_returns_neither_text_nor_tools()
    {
        var handler = new JsonHandler("""
            {"choices":[{"message":{"content":null}}]}
            """, HttpStatusCode.OK);

        var exception = await Assert.ThrowsAsync<AiUpstreamException>(() =>
            Create(handler, "secret").CompleteAsync(Request, CancellationToken.None));

        Assert.Equal(AiErrorCodes.UnsupportedUpstreamResponse, exception.Code);
    }

    [Fact]
    public async Task Maps_upstream_status_and_does_not_send_report_secret()
    {
        var handler = new JsonHandler("upstream failure", HttpStatusCode.BadGateway);

        var exception = await Assert.ThrowsAsync<AiUpstreamException>(() =>
            Create(handler, "chat-secret").CompleteAsync(Request, CancellationToken.None));

        Assert.Equal(AiErrorCodes.UpstreamFailure, exception.Code);
        Assert.Equal(502, exception.UpstreamStatus);
        Assert.DoesNotContain("report-secret", handler.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Maps_transport_timeout_to_stable_timeout_error()
    {
        var handler = new BlockingHandler();
        var exception = await Assert.ThrowsAsync<AiUpstreamException>(() =>
            Create(handler, "chat-secret").CompleteAsync(Request, CancellationToken.None));

        Assert.Equal(AiErrorCodes.UpstreamTimeout, exception.Code);
    }

    private static OpenAiCompatibleIocChatClient Create(HttpMessageHandler handler, string? key)
    {
        return new OpenAiCompatibleIocChatClient(
            new HttpClient(handler),
            new StubSecretStore(key),
            new StubLogger());
    }

    private sealed class JsonHandler(string body, HttpStatusCode status) : HttpMessageHandler
    {
        public string? Authorization { get; private set; }
        public string Body { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Authorization = request.Headers.Authorization?.ToString();
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class BlockingHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Yield();
            throw new OperationCanceledException();
        }
    }

    private sealed class StubSecretStore(string? key) : ISecretStore
    {
        public Task<ManagedSecretStatus?> GetStatusAsync(string name, CancellationToken cancellationToken) =>
            Task.FromResult<ManagedSecretStatus?>(key is null ? null : new ManagedSecretStatus(name, true, 1, null));

        public Task<string?> ResolveAsync(string name, CancellationToken cancellationToken) => Task.FromResult(key);
        public Task<SecretMutationResult> SetAsync(string name, string value, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> ClearAsync(string name, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class StubLogger : Microsoft.Extensions.Logging.ILogger<OpenAiCompatibleIocChatClient>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => false;
        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }
}
