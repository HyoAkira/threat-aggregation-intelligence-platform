using System.Net;
using ThreatIntelligence.Application.Abstractions.Secrets;
using ThreatIntelligence.Application.AI;
using ThreatIntelligence.Infrastructure.AI;
using Xunit;

namespace ThreatIntelligence.UnitTests.AI;

/// <summary>
/// Log-capture tests for the AI feature: log records may contain identifiers, statuses, and
/// timing only. Prompt bodies, evidence snapshots, report text, authorization values, credential
/// values, and upstream error bodies must never appear.
/// </summary>
public sealed class AiTelemetryTests
{
    private const string UpstreamSecretBody = "SECRET-UPSTREAM-ERROR-BODY";
    private const string ReportSecretText = "gizli rapor metni";
    private const string EvidenceSecretValue = "secret-ioc-value";
    private const string PolicySecretText = "Türkçe yönerge metni";
    private const string ApiKeyValue = "sk-test-api-key-value";
    private const string SseDelta =
        "data: {\"choices\":[{\"delta\":{\"content\":\"## Yönetici Özeti\\nGizli rapor metni.\"}}]}\n\n" +
        "data: [DONE]\n\n";

    [Fact]
    public async Task Upstream_failure_logs_only_the_status_code()
    {
        var capture = new LogCapture();
        var client = new OpenAiCompatibleChatClient(
            new HttpClient(new StaticHandler(HttpStatusCode.InternalServerError, UpstreamSecretBody)),
            new StubSecretStore(ApiKeyValue),
            AiClientOptions.Default,
            capture);

        var exception = await Record.ExceptionAsync(async () =>
            await Enumerate(client.StreamAsync(ChatRequest(), CancellationToken.None)));

        Assert.NotNull(exception);
        var allText = string.Join('\n', capture.Entries);
        Assert.Contains("500", allText);
        Assert.DoesNotContain(UpstreamSecretBody, allText);
        Assert.DoesNotContain(ApiKeyValue, allText);
    }

    [Fact]
    public async Task Client_authorization_value_never_reaches_logs()
    {
        var capture = new LogCapture();
        var client = new OpenAiCompatibleChatClient(
            new HttpClient(new StaticHandler(HttpStatusCode.Unauthorized, UpstreamSecretBody)),
            new StubSecretStore(ApiKeyValue),
            AiClientOptions.Default,
            capture);

        var exception = await Record.ExceptionAsync(async () =>
            await Enumerate(client.StreamAsync(ChatRequest(), CancellationToken.None)));

        Assert.IsType<AiUpstreamException>(exception);
        Assert.All(capture.Entries, entry =>
        {
            Assert.DoesNotContain(ApiKeyValue, entry);
            Assert.DoesNotContain("Bearer", entry);
            Assert.DoesNotContain(UpstreamSecretBody, entry);
        });
        Assert.DoesNotContain(ApiKeyValue, exception!.Message);
    }

    [Fact]
    public async Task Successful_stream_logs_contain_no_prompt_or_output_text()
    {
        var capture = new LogCapture();
        var evidence = new IocEvidenceSnapshot(
            7, EvidenceSecretValue, "url", "secret.example", "secret.example",
            "2026-01-01T08:00:00Z", null, [], 0);
        var policy = new AiReportPolicySnapshot(4, PolicySecretText);
        var request = new ChatCompletionRequest(
            "http://modelserver:11434",
            "test-model",
            AiReportMessageBuilder.Build(policy, evidence),
            0.2,
            4000);

        var client = new OpenAiCompatibleChatClient(
            new HttpClient(new StaticHandler(HttpStatusCode.OK, SseDelta)),
            new StubSecretStore(ApiKeyValue),
            AiClientOptions.Default,
            capture);

        var deltas = new List<string>();
        await foreach (var delta in client.StreamAsync(request, CancellationToken.None))
        {
            deltas.Add(delta.Text);
        }

        var allText = string.Join('\n', capture.Entries);
        Assert.DoesNotContain(ReportSecretText, allText);
        Assert.DoesNotContain(EvidenceSecretValue, allText);
        Assert.DoesNotContain(PolicySecretText, allText);
        Assert.DoesNotContain(ApiKeyValue, allText);
        Assert.NotEmpty(deltas);
    }

    private static ChatCompletionRequest ChatRequest() => new(
        "http://modelserver:11434",
        "test-model",
        [new ChatCompletionMessage("system", "safety"), new ChatCompletionMessage("user", "evidence")],
        0.2,
        4000);

    private static async Task Enumerate(IAsyncEnumerable<ChatCompletionDelta> stream)
    {
        await foreach (var _ in stream)
        {
        }
    }

    private sealed class StaticHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8,
                    status == HttpStatusCode.OK ? "text/event-stream" : "text/plain"),
            });
    }

    private sealed class StubSecretStore(string? value) : ISecretStore
    {
        public Task<ManagedSecretStatus?> GetStatusAsync(string name, CancellationToken cancellationToken) =>
            Task.FromResult<ManagedSecretStatus?>(new ManagedSecretStatus(name, true, 1, null));

        public Task<string?> ResolveAsync(string name, CancellationToken cancellationToken) =>
            Task.FromResult(value);

        public Task<SecretMutationResult> SetAsync(string name, string value, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> ClearAsync(string name, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    /// <summary>Captures rendered log messages for content assertions.</summary>
    private sealed class LogCapture : Microsoft.Extensions.Logging.ILogger<OpenAiCompatibleChatClient>
    {
        internal List<string> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

        public void Log<TState>(
            Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId,
            TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add(formatter(state, exception));
        }
    }
}
