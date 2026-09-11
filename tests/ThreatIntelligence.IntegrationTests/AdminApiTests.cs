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
/// End-to-end Admin authorization for the E10 read endpoints. The <c>api/admin</c> controller
/// convention must require the Admin role for every route under that prefix.
/// </summary>
[Collection("sqlserver")]
public sealed class AdminApiTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private const string Issuer = "threat-intelligence-tests";
    private const string Audience = "threat-intelligence-api-tests";
    private const string Secret = "integration-test-jwt-secret-that-is-at-least-sixty-four-bytes-long!!";

    private AdminApiFactory _factory = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await fixture.ResetDataAsync();
        _factory = new AdminApiFactory(fixture.ConnectionString);
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
    public async Task Non_admin_cannot_read_the_provider_dashboard()
    {
        using var response = await GetAuthorized("/api/admin/providers", UserRole.User);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var problem = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal("forbidden", (string)problem["code"]!);
    }

    [Fact]
    public async Task Admin_can_read_the_provider_dashboard_and_summary()
    {
        using var providers = await GetAuthorized("/api/admin/providers", UserRole.Admin);
        Assert.Equal(HttpStatusCode.OK, providers.StatusCode);
        var items = (await providers.Content.ReadFromJsonAsync<JsonArray>())!;
        Assert.NotEmpty(items!);

        using var summary = await GetAuthorized("/api/admin/summary", UserRole.Admin);
        Assert.Equal(HttpStatusCode.OK, summary.StatusCode);
        var body = (await summary.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.NotNull(body["total"]);
    }

    [Fact]
    public async Task Logs_reject_invalid_count_and_level()
    {
        using var oversizedRequest = new HttpRequestMessage(HttpMethod.Get, "/api/admin/logs?count=600");
        oversizedRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(UserRole.Admin));
        oversizedRequest.Headers.Add("X-Correlation-ID", "admin-logs-400");
        using var oversized = await _client.SendAsync(oversizedRequest);
        Assert.Equal(HttpStatusCode.BadRequest, oversized.StatusCode);
        Assert.Equal("application/problem+json", oversized.Content.Headers.ContentType?.MediaType);
        var problem = (await oversized.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal("invalid_log_query", (string)problem["code"]!);
        Assert.Equal("urn:threat-intelligence:problem:invalid_log_query", (string)problem["type"]!);
        Assert.Equal("admin-logs-400", (string)problem["correlationId"]!);
        Assert.Equal(400, (int)problem["status"]!);

        using var badLevel = await GetAuthorized("/api/admin/logs?level=Trace", UserRole.Admin);
        Assert.Equal(HttpStatusCode.BadRequest, badLevel.StatusCode);

        using var valid = await GetAuthorized("/api/admin/logs", UserRole.Admin);
        Assert.Equal(HttpStatusCode.OK, valid.StatusCode);
    }

    [Fact]
    public async Task Api_routing_errors_are_problem_details()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/does-not-exist");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(UserRole.Admin));
        request.Headers.Add("X-Correlation-ID", "route-404");
        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("route-404", response.Headers.GetValues("X-Correlation-ID").Single());
        var problem = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal("route_not_found", (string)problem["code"]!);
        Assert.Equal("urn:threat-intelligence:problem:route_not_found", (string)problem["type"]!);
        Assert.Equal("route-404", (string)problem["correlationId"]!);
        Assert.Equal(404, (int)problem["status"]!);
    }

    [Fact]
    public async Task OpenApi_describes_the_shared_problem_details_contract()
    {
        using var response = await _client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = (await JsonNode.ParseAsync(await response.Content.ReadAsStreamAsync()))!.AsObject();
        var problemSchema = document["components"]!["schemas"]!["ApiProblemDetails"]!.AsObject();
        Assert.Equal("object", (string)problemSchema["type"]!);
        Assert.Contains("code", problemSchema["properties"]!.AsObject().Select(property => property.Key));
        Assert.Contains("correlationId", problemSchema["properties"]!.AsObject().Select(property => property.Key));

        var search = document["paths"]!["/api/iocs/search"]!["get"]!.AsObject();
        var badRequest = search["responses"]!["400"]!.AsObject();
        Assert.NotNull(badRequest["content"]!["application/problem+json"]!["schema"]!["$ref"]);
    }

    [Fact]
    public async Task OpenApi_describes_the_bearer_security_scheme()
    {
        using var response = await _client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = (await JsonNode.ParseAsync(await response.Content.ReadAsStreamAsync()))!.AsObject();

        var scheme = document["components"]!["securitySchemes"]!["Bearer"]!.AsObject();
        Assert.Equal("http", (string)scheme["type"]!);
        Assert.Equal("bearer", (string)scheme["scheme"]!);
        Assert.Equal("JWT", (string)scheme["bearerFormat"]!);

        var documentSecurity = document["security"]!.AsArray();
        Assert.Contains(
            documentSecurity,
            requirement => requirement!.AsObject()["Bearer"] is not null);

        var anonymousOperation = document["paths"]!["/api/auth/login"]!["post"]!.AsObject();
        var anonymousSecurity = anonymousOperation["security"]?.AsArray();
        Assert.NotNull(anonymousSecurity);
        Assert.Empty(anonymousSecurity);
    }

    private Task<HttpResponseMessage> GetAuthorized(string path, UserRole role)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
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

    private sealed class AdminApiFactory : WebApplicationFactory<Program>
    {
        private readonly Dictionary<string, string?> _previousEnvironment;

        public AdminApiFactory(string connectionString)
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
                ["LOGGING__MINIMUMLEVEL"] = "Information"
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
