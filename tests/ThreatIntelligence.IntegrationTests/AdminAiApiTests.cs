using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using ThreatIntelligence.Domain.Enums;

namespace ThreatIntelligence.IntegrationTests;

/// <summary>
/// Verifies that the AI admin endpoints inherit the api/admin Admin-role requirement and that
/// a full AI settings flow works end to end through the HTTP pipeline.
/// </summary>
[Collection("sqlserver")]
public sealed class AdminAiApiTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private const string Issuer = "threat-intelligence-tests";
    private const string Audience = "threat-intelligence-api-tests";
    private const string Secret = "integration-test-jwt-secret-that-is-at-least-sixty-four-bytes-long!!";

    private AdminAiApiFactory _factory = null!;
    private HttpClient _client = null!;
    private string _connectionString = null!;

    public async Task InitializeAsync()
    {
        await fixture.ResetDataAsync();
        _connectionString = fixture.ConnectionString;
        _factory = new AdminAiApiFactory(_connectionString);
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
        using var startup = await _client.GetAsync("/health/live");
        startup.EnsureSuccessStatusCode();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Anonymous_caller_cannot_read_or_change_ai_settings()
    {
        using var read = await _client.GetAsync("/api/admin/ai/settings");
        Assert.Equal(HttpStatusCode.Unauthorized, read.StatusCode);
        var problem = (await read.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal("unauthenticated", (string)problem["code"]!);

        using var change = await _client.PutAsJsonAsync("/api/admin/ai/settings",
            new { baseUrl = "http://ollama:11434", modelId = "llama3.1", enabled = true });
        Assert.Equal(HttpStatusCode.Unauthorized, change.StatusCode);
    }

    [Fact]
    public async Task Non_admin_cannot_read_or_change_ai_settings()
    {
        using var read = await GetAuthorized("/api/admin/ai/settings", UserRole.User);
        Assert.Equal(HttpStatusCode.Forbidden, read.StatusCode);
        var problem = (await read.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal("forbidden", (string)problem["code"]!);

        using var change = await PutAuthorized("/api/admin/ai/settings",
            new { baseUrl = "http://ollama:11434", modelId = "llama3.1", enabled = true }, UserRole.User);
        Assert.Equal(HttpStatusCode.Forbidden, change.StatusCode);
    }

    [Fact]
    public async Task Non_admin_cannot_read_or_publish_the_report_policy()
    {
        using var read = await GetAuthorized("/api/admin/ai/report-policy", UserRole.User);
        Assert.Equal(HttpStatusCode.Forbidden, read.StatusCode);

        using var publish = await PutAuthorized("/api/admin/ai/report-policy",
            new { text = "Yönerge", expectedVersion = (int?)null }, UserRole.User);
        Assert.Equal(HttpStatusCode.Forbidden, publish.StatusCode);
    }

    [Fact]
    public async Task Admin_saves_and_reads_ai_settings_through_the_api()
    {
        using var save = await PutAuthorized("/api/admin/ai/settings",
            new { baseUrl = "http://ollama:11434", modelId = "llama3.1", enabled = true }, UserRole.Admin);
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);
        var saved = (await save.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal("http://ollama:11434", (string)saved["baseUrl"]!);
        Assert.False((bool)saved["apiKeyConfigured"]!);

        using var read = await GetAuthorized("/api/admin/ai/settings", UserRole.Admin);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        var body = (await read.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal("http://ollama:11434", (string)body["baseUrl"]!);
        Assert.Equal("llama3.1", (string)body["modelId"]!);
        Assert.True((bool)body["enabled"]!);
    }

    [Fact]
    public async Task Invalid_save_returns_problem_details_and_preserves_previous_settings()
    {
        using var first = await PutAuthorized("/api/admin/ai/settings",
            new { baseUrl = "http://ollama:11434", modelId = "llama3.1", enabled = true }, UserRole.Admin);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        using var invalid = await PutAuthorized("/api/admin/ai/settings",
            new { baseUrl = "not-a-url", modelId = "llama3.1", enabled = true }, UserRole.Admin);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal("application/problem+json", invalid.Content.Headers.ContentType?.MediaType);
        var problem = (await invalid.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal("invalid_ai_settings", (string)problem["code"]!);

        using var read = await GetAuthorized("/api/admin/ai/settings", UserRole.Admin);
        var body = (await read.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal("http://ollama:11434", (string)body["baseUrl"]!);
    }

    private Task<HttpResponseMessage> GetAuthorized(string path, UserRole role)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(role));
        return _client.SendAsync(request);
    }

    private Task<HttpResponseMessage> PutAuthorized(string path, object payload, UserRole role)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, path)
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(role));
        return _client.SendAsync(request);
    }

    private static string CreateToken(UserRole role)
    {
        var now = DateTimeOffset.UtcNow;
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = Audience,
            NotBefore = now.UtcDateTime,
            Expires = now.AddHours(1).UtcDateTime,
            Subject = new ClaimsIdentity([
                new Claim(ClaimTypes.Name, $"admin-api-{role}"),
                new Claim(ClaimTypes.Role, role.ToString())
            ]),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)),
                SecurityAlgorithms.HmacSha256)
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    private sealed class AdminAiApiFactory : WebApplicationFactory<Program>
    {
        private readonly Dictionary<string, string?> _previousEnvironment;

        public AdminAiApiFactory(string connectionString)
        {
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
                ["ENCRYPTION__KEYVERSION"] = "1"
            };

            _previousEnvironment = environment.Keys.ToDictionary(key => key, Environment.GetEnvironmentVariable);
            foreach (var (key, value) in environment)
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Testing");

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            foreach (var (key, value) in _previousEnvironment)
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }
}
