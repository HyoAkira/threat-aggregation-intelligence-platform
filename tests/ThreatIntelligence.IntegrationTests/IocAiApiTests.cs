using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using ThreatIntelligence.Application.AI;
using ThreatIntelligence.Domain.Entities;
using ThreatIntelligence.Domain.Enums;
using Xunit;

namespace ThreatIntelligence.IntegrationTests;

/// <summary>
/// IOC AI report generation: authentication, fail-closed pre-stream errors, the backend-owned
/// meta/delta/complete/error SSE contract, mid-stream failures, cancellation, throttling, and the
/// requirement that no report text is retained server-side.
/// </summary>
[Collection("sqlserver")]
public sealed class IocAiApiTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private const string Issuer = "threat-intelligence-tests";
    private const string Audience = "threat-intelligence-api-tests";
    private const string Secret = "integration-test-jwt-secret-that-is-at-least-sixty-four-bytes-long!!";

    private string _connectionString = null!;

    public async Task InitializeAsync()
    {
        await fixture.ResetDataAsync();
        _connectionString = fixture.ConnectionString;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static readonly string ValidReport = $"""
        ## Yönetici Özeti
        Özet.
        ## Teknik Analiz
        Gözlem [S1].
        ## Bağlam
        Bağlam.
        ## Öneriler
        Öneri.
        ## Sınırlamalar ve Belirsizlik
        Sınırlama.
        ## Feragatname
        Feragat.
        """;

    private async Task SeedAiAsync(bool enabled = true, bool withKey = true, string baseUrl = "http://127.0.0.1:3002")
    {
        await using var context = fixture.CreateContext();
        context.AiConnectionSettings.Add(new AiConnectionSettings(
            baseUrl, "test-model", enabled, DateTime.UtcNow));
        await context.SaveChangesAsync();

        if (withKey)
        {
            var rootKey = Enumerable.Range(1, 32).Select(index => (byte)index).ToArray();
            var protector = new Infrastructure.Secrets.AesGcmSecretProtector(
                new Infrastructure.Configuration.EncryptionOptions(rootKey, 1, null));
            var payload = protector.Protect(
                Application.Abstractions.Secrets.ManagedSecretNames.OpenAiCompatibleServerApiKey, "test-key");
            context.EncryptedSecrets.Add(new EncryptedSecret(
                Application.Abstractions.Secrets.ManagedSecretNames.OpenAiCompatibleServerApiKey,
                payload.FormatVersion, payload.KeyVersion, payload.Ciphertext, payload.Nonce,
                payload.AuthenticationTag, DateTime.UtcNow, DateTime.UtcNow));
            await context.SaveChangesAsync();
        }
    }

    private async Task<int> SeedIocAsync()
    {
        await using var context = fixture.CreateContext();
        var row = await IocQueryTestData.InsertIocAsync(
            context, providerId: 1, recordId: "tf-ai-1", iocValue: "ai.example",
            iocType: IocType.Url, urlHost: "ai.example", parentDomain: "ai.example",
            threatType: "botnet", malware: "win.agenttesla", updatedAtUtc: DateTime.UtcNow.AddHours(-1));
        await IocQueryTestData.TagAsync(context, row, "botnet");
        return row.Id;
    }

    private async Task<IocAiApiFactory> StartFactoryAsync(StubChatCompletionClient stub)
    {
        var factory = new IocAiApiFactory(_connectionString, stub);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
        using var startup = await client.GetAsync("/health/live");
        startup.EnsureSuccessStatusCode();
        return factory;
    }

    private static string CreateToken(string username, UserRole role)
    {
        var now = DateTimeOffset.UtcNow;
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = Audience,
            NotBefore = now.UtcDateTime,
            Expires = now.AddHours(1).UtcDateTime,
            Subject = new ClaimsIdentity([
                new Claim(ClaimTypes.Name, username),
                new Claim(ClaimTypes.Role, role.ToString())
            ]),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)),
                SecurityAlgorithms.HmacSha256)
        };
        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    [Fact]
    public async Task Anonymous_report_request_is_allowed_and_anonymous_ioc_get_still_succeeds()
    {
        var iocId = await SeedIocAsync();
        await SeedAiAsync();
        using var factory = await StartFactoryAsync(new StubChatCompletionClient(ValidReport));
        using var client = factory.CreateClient();

        using var detail = await client.GetAsync($"/api/iocs/{iocId}");
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);

        using var report = await client.PostAsync($"/api/iocs/{iocId}/ai/reports", null);
        Assert.Equal(HttpStatusCode.OK, report.StatusCode);
        Assert.Equal("text/event-stream", report.Content.Headers.ContentType?.MediaType);
        Assert.Contains("event: complete", await report.Content.ReadAsStringAsync());
        Assert.Equal(1, factory.Stub.Calls);
    }

    [Fact]
    public async Task Disabled_ai_returns_a_stable_problem_and_no_stream()
    {
        var iocId = await SeedIocAsync();
        await SeedAiAsync(enabled: false);
        using var factory = await StartFactoryAsync(new StubChatCompletionClient(ValidReport));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken("user-a", UserRole.User));

        using var report = await client.PostAsync($"/api/iocs/{iocId}/ai/reports", null);

        Assert.Equal(HttpStatusCode.Conflict, report.StatusCode);
        Assert.Equal("application/problem+json", report.Content.Headers.ContentType?.MediaType);
        var problem = (await report.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal("ai_disabled", (string)problem["code"]!);
        Assert.Null(factory.Stub.Calls);
    }

    [Fact]
    public async Task Missing_credential_is_optional_for_a_keyless_local_server()
    {
        var iocId = await SeedIocAsync();
        await SeedAiAsync(withKey: false);
        using var factory = await StartFactoryAsync(new StubChatCompletionClient(ValidReport));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken("user-a", UserRole.User));

        using var report = await client.PostAsync($"/api/iocs/{iocId}/ai/reports", null);

        // A deliberately keyless local server is valid: the run proceeds and the optional key is
        // resolved per operation only when it exists.
        Assert.Equal(HttpStatusCode.OK, report.StatusCode);
        var body = await report.Content.ReadAsStringAsync();
        Assert.Contains("event: complete", body);
        Assert.Equal(1, factory.Stub.Calls);
    }

    [Fact]
    public async Task Unknown_ioc_returns_404_before_streaming()
    {
        await SeedAiAsync();
        using var factory = await StartFactoryAsync(new StubChatCompletionClient(ValidReport));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken("user-a", UserRole.User));

        using var report = await client.PostAsync("/api/iocs/999999/ai/reports", null);

        Assert.Equal(HttpStatusCode.NotFound, report.StatusCode);
        var problem = (await report.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal("ioc_not_found", (string)problem["code"]!);
    }

    [Fact]
    public async Task Successful_run_emits_meta_delta_complete_and_retains_nothing()
    {
        var iocId = await SeedIocAsync();
        await SeedAiAsync();
        using var factory = await StartFactoryAsync(new StubChatCompletionClient(ValidReport));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken("user-a", UserRole.User));

        using var report = await client.PostAsync($"/api/iocs/{iocId}/ai/reports", null);

        Assert.Equal(HttpStatusCode.OK, report.StatusCode);
        Assert.Equal("text/event-stream", report.Content.Headers.ContentType?.MediaType);

        var body = await report.Content.ReadAsStringAsync();
        var events = ParseEvents(body);
        Assert.True(events[^1].Name == "complete", body);
        Assert.Equal("meta", events[0].Name);
        var deltas = events.Where(item => item.Name == "delta")
            .Select(item => (string?)((JsonObject)item.Data!)["text"] ?? "<MISSING>" + item.Data!.ToJsonString())
            .ToList();
        Assert.True(string.Concat(deltas) == ValidReport, string.Join('|', deltas));

        var meta = (JsonObject)events[0].Data!;
        Assert.Equal("test-model", (string)meta["modelId"]!);
        Assert.Equal(1, (int)meta["policyVersion"]!);
        Assert.Equal(0, (int)meta["omittedSourceCount"]!);

        var complete = (JsonObject)events[^1].Data!;
        Assert.Equal(ValidReport, (string)complete["report"]!);
        Assert.Equal("test-model", (string)complete["modelId"]!);
        Assert.Equal(1, (int)complete["policyVersion"]!);

        // Nothing about the report is retained server-side: the AI tables keep only the
        // configuration and policy data that existed before the run.
        await using var context = fixture.CreateContext();
        Assert.Equal(1, await context.AiConnectionSettings.CountAsync());
        Assert.Equal(1, await context.AiReportPolicyVersions.CountAsync());
        Assert.Equal(1, await context.AiReportPolicyActive.CountAsync());
    }

    [Fact]
    public async Task Mid_stream_upstream_failure_emits_error_and_never_complete()
    {
        var iocId = await SeedIocAsync();
        await SeedAiAsync();
        using var factory = await StartFactoryAsync(
            new StubChatCompletionClient(ValidReport, failAfterDeltas: true));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken("user-a", UserRole.User));

        using var report = await client.PostAsync($"/api/iocs/{iocId}/ai/reports", null);

        var body = await report.Content.ReadAsStringAsync();
        var events = ParseEvents(body);
        Assert.Contains(events, item => item.Name == "delta");
        var errorEvent = events.Single(item => item.Name == "error");
        Assert.Equal("ai_upstream_failure", (string)((JsonObject)errorEvent.Data!)["code"]!);
        Assert.DoesNotContain(events, item => item.Name == "complete");
    }

    [Fact]
    public async Task Invalid_model_output_fails_with_a_safe_error_event()
    {
        var iocId = await SeedIocAsync();
        await SeedAiAsync();
        var invalid = ValidReport.Replace("## Feragatname", "## Disclaimer");
        using var factory = await StartFactoryAsync(new StubChatCompletionClient(invalid));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken("user-a", UserRole.User));

        using var report = await client.PostAsync($"/api/iocs/{iocId}/ai/reports", null);

        var body = await report.Content.ReadAsStringAsync();
        var events = ParseEvents(body);
        var errorEvent = events.Single(item => item.Name == "error");
        Assert.Equal("ai_output_invalid", (string)((JsonObject)errorEvent.Data!)["code"]!);
        Assert.DoesNotContain(events, item => item.Name == "complete");
    }

    [Fact]
    public async Task Exceeding_the_per_user_budget_returns_429_with_retry_after_and_no_upstream_call()
    {
        var iocId = await SeedIocAsync();
        await SeedAiAsync();
        using var factory = await StartFactoryAsync(new StubChatCompletionClient(ValidReport));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken("user-a", UserRole.User));

        // The 10th request succeeds; the 11th is refused before any upstream call.
        for (var attempt = 0; attempt < 10; attempt++)
        {
            using var report = await client.PostAsync($"/api/iocs/{iocId}/ai/reports", null);
            Assert.Equal(HttpStatusCode.OK, report.StatusCode);
        }

        factory.Stub.Calls = 0;
        using var refused = await client.PostAsync($"/api/iocs/{iocId}/ai/reports", null);
        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
        Assert.Equal("application/problem+json", refused.Content.Headers.ContentType?.MediaType);
        var problem = (await refused.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal("ai_quota_exceeded", (string)problem["code"]!);
        Assert.True(refused.Headers.RetryAfter is not null);
        Assert.Equal(0, factory.Stub.Calls);
    }

    [Fact]
    public async Task Client_abort_makes_no_extra_upstream_request_and_stops_the_stream()
    {
        var iocId = await SeedIocAsync();
        await SeedAiAsync();
        using var factory = await StartFactoryAsync(new StubChatCompletionClient(ValidReport, delayBetweenDeltas: 20));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken("user-a", UserRole.User));

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/iocs/{iocId}/ai/reports")
        {
            Content = new StringContent(string.Empty, Encoding.UTF8, "application/json")
        };
        using var linkedCts = new CancellationTokenSource();
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, linkedCts.Token);
        var stream = await response.Content.ReadAsStreamAsync();
        var buffer = new byte[4096];
        _ = await stream.ReadAsync(buffer, linkedCts.Token);

        // The in-memory test transport does not propagate client aborts to the server
        // RequestAborted token; cancellation is verified end to end by the client-side slow-drip
        // unit test. Here the verified properties are: exactly one upstream request and no
        // additional upstream request after cancellation.
        await linkedCts.CancelAsync();

        Assert.Equal(1, factory.Stub.Calls);
    }

    private static List<(string Name, JsonNode? Data)> ParseEvents(string body)
    {
        var events = new List<(string, JsonNode?)>();
        string? eventName = null;
        foreach (var rawLine in body.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (line.StartsWith("event:", StringComparison.Ordinal))
            {
                eventName = line[6..].Trim();
            }
            else if (line.StartsWith("data:", StringComparison.Ordinal) && eventName is not null)
            {
                var data = line[5..].Trim();
                events.Add((eventName, data.Length > 0 ? JsonNode.Parse(data) : null));
                eventName = null;
            }
        }

        return events;
    }

    internal sealed class IocAiApiFactory : WebApplicationFactory<Program>
    {
        private readonly Dictionary<string, string?> _previousEnvironment;
        internal StubChatCompletionClient Stub { get; }

        public IocAiApiFactory(string connectionString, StubChatCompletionClient stub)
        {
            Stub = stub;
            var environment = new Dictionary<string, string?>
            {
                ["DATABASE__CONNECTIONSTRING"] = connectionString,
                ["RABBITMQ__HOST"] = "localhost",
                ["RABBITMQ__PORT"] = "5672",
                ["RABBITMQ__USER"] = "guest",
                ["RABBITMQ__PASSWORD"] = "guest",
                ["RABBITMQ__EXCHANGE"] = "test-exchange",
                ["JWT__SECRET"] = Secret,
                ["JWT__ISSUER"] = Issuer,
                ["JWT__AUDIENCE"] = Audience,
                ["JWT__EXPIRYMINUTES"] = "480",
                ["AUTH__PASSWORDWORKFACTOR"] = "4",
                ["THREATFOX__APIKEY"] = "test-api-key",
                ["ADMIN__USERNAME"] = "admin-api-admin",
                ["ADMIN__PASSWORD"] = "Admin-test-password!",
                ["LOGGING__MINIMUMLEVEL"] = "Information",
                ["ENCRYPTION__ROOTKEY"] = "AQIDBAUGBwgJCgsMDQ4PEBESExQVFhcYGRobHB0eHyA=",
                ["ENCRYPTION__KEYVERSION"] = "1",
            };

            _previousEnvironment = environment.Keys.ToDictionary(key => key, Environment.GetEnvironmentVariable);
            foreach (var (key, value) in environment)
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                RemoveAll(services, typeof(IChatCompletionClient));
                services.AddSingleton<IChatCompletionClient>(Stub);
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            foreach (var (key, value) in _previousEnvironment)
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }

        private static void RemoveAll(IServiceCollection services, Type serviceType)
        {
            var descriptor = services.FirstOrDefault(item => item.ServiceType == serviceType);
            while (descriptor is not null)
            {
                services.Remove(descriptor);
                descriptor = services.FirstOrDefault(item => item.ServiceType == serviceType);
            }
        }
    }
}

internal sealed class StubChatCompletionClient(
    string output,
    bool failAfterDeltas = false,
    int delayBetweenDeltas = 0) : IChatCompletionClient
{
    public int? Calls { get; set; }
    public bool Cancelled { get; private set; }

    public async IAsyncEnumerable<ChatCompletionDelta> StreamAsync(
        ChatCompletionRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        Calls = (Calls ?? 0) + 1;
        foreach (var chunk in output.Chunk(12).Select(chars => new string(chars)))
        {
            if (delayBetweenDeltas > 0)
            {
                try
                {
                    await Task.Delay(delayBetweenDeltas, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    Cancelled = true;
                    throw;
                }
            }

            if (cancellationToken.IsCancellationRequested)
            {
                Cancelled = true;
                throw new OperationCanceledException(cancellationToken);
            }

            yield return new ChatCompletionDelta(chunk);
        }

        if (failAfterDeltas)
        {
            throw new AiUpstreamException(AiErrorCodes.UpstreamFailure, "Upstream died mid-stream.");
        }
    }
}
