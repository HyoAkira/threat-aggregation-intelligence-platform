using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using ThreatIntelligence.API.Errors;
using ThreatIntelligence.Domain.Entities;
using ThreatIntelligence.Domain.Enums;

namespace ThreatIntelligence.IntegrationTests;

[Collection("sqlserver")]
public sealed class IocsApiTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private const string Issuer = "threat-intelligence-tests";
    private const string Audience = "threat-intelligence-api-tests";
    private const string Secret = "integration-test-jwt-secret-that-is-at-least-sixty-four-bytes-long!!";

    private IocApiFactory _factory = null!;
    private HttpClient _client = null!;
    private IoC _ipv4;
    private IoC _url;

    public async Task InitializeAsync()
    {
        await fixture.ResetDataAsync();
        _factory = new IocApiFactory(fixture.ConnectionString);
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        using var startup = await _client.GetAsync("/health/live");
        startup.EnsureSuccessStatusCode();

        await using var context = fixture.CreateContext();
        _ipv4 = await IocQueryTestData.InsertIocAsync(
            context, 1, "api-ip-1", "1.2.3.4", IocType.Ipv4, lastSeenUtc: DateTime.UtcNow.AddDays(-1));
        await IocQueryTestData.InsertIocAsync(
            context, 2, "api-ip-2", "1.2.3.4", IocType.Ipv4, lastSeenUtc: DateTime.UtcNow.AddDays(-2));
        _url = await IocQueryTestData.InsertIocAsync(
            context, 1, "api-url-1", "https://evil.example/x", IocType.Url,
            urlHost: "evil.example", parentDomain: "evil.example");
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Unauthenticated_request_is_a_401_problem_with_the_correlation_id()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/iocs");
        request.Headers.Add("X-Correlation-ID", "corr-401");
        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("corr-401", response.Headers.GetValues("X-Correlation-ID").Single());
        var problem = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal("unauthenticated", (string)problem["code"]!);
        Assert.Equal("corr-401", (string)problem["correlationId"]!);
        Assert.Equal(401, (int)problem["status"]!);
    }

    [Fact]
    public async Task Search_groups_sources_across_providers()
    {
        using var response = await GetAuthorized("/api/iocs/search?q=1.2.3.4");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var group = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal("ipv4", (string)group["iocType"]!);
        Assert.Equal(2, group["sources"]!.AsArray().Count);
    }

    [Fact]
    public async Task Search_invalid_input_is_400_and_absent_is_404()
    {
        using var invalid = await GetAuthorized("/api/iocs/search?q=not+an+ioc+at+all");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var invalidProblem = (await invalid.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal("invalid_input", (string)invalidProblem["code"]!);

        using var absent = await GetAuthorized("/api/iocs/search?q=absent.example");
        Assert.Equal(HttpStatusCode.NotFound, absent.StatusCode);
        var absentProblem = (await absent.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal("ioc_not_found", (string)absentProblem["code"]!);
    }

    [Fact]
    public async Task Detail_returns_group_or_404_and_rejects_non_numeric()
    {
        using var ok = await GetAuthorized($"/api/iocs/{_url.Id}");
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);

        using var missing = await GetAuthorized("/api/iocs/987654");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        using var malformed = await GetAuthorized("/api/iocs/not-a-number");
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
    }

    [Fact]
    public async Task Browse_rejects_invalid_filters_and_oversized_pages()
    {
        using var oversized = await GetAuthorized("/api/iocs?pageSize=101");
        Assert.Equal(HttpStatusCode.BadRequest, oversized.StatusCode);

        using var badType = await GetAuthorized("/api/iocs?type=email");
        Assert.Equal(HttpStatusCode.BadRequest, badType.StatusCode);

        using var badOrder = await GetAuthorized("/api/iocs?orderBy=providerRecordId");
        Assert.Equal(HttpStatusCode.BadRequest, badOrder.StatusCode);

        using var ok = await GetAuthorized("/api/iocs");
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var envelope = (await ok.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal(20, (int)envelope["pageSize"]!);
    }

    [Fact]
    public async Task Relations_reject_ip_groups_and_unknown_ids()
    {
        using var ip = await GetAuthorized($"/api/iocs/{_ipv4.Id}/relations?relation=urlHost");
        Assert.Equal(HttpStatusCode.BadRequest, ip.StatusCode);
        var problem = (await ip.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal("ioc_relation_not_supported", (string)problem["code"]!);

        using var unknown = await GetAuthorized("/api/iocs/987654/relations?relation=urlHost");
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    [Fact]
    public async Task Guarded_conflict_maps_to_409()
    {
        using var response = await _client.GetAsync("/api/test/conflict");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal("fetch_conflict", (string)problem["code"]!);
    }

    [Fact]
    public async Task Non_admin_on_an_admin_route_is_a_403_problem()
    {
        using var response = await GetAuthorized("/api/admin/ioc-test");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var problem = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal("forbidden", (string)problem["code"]!);
    }

    [Fact]
    public async Task Unexpected_error_maps_to_a_safe_500()
    {
        using var response = await _client.GetAsync("/api/test/boom");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("secret connection string", body, StringComparison.Ordinal);
        Assert.DoesNotContain("InvalidOperationException", body, StringComparison.Ordinal);
        var problem = JsonNode.Parse(body)!.AsObject();
        Assert.Equal("server_error", (string)problem["code"]!);
    }

    private Task<HttpResponseMessage> GetAuthorized(string path)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken());
        return _client.SendAsync(request);
    }

    private static string CreateToken()
    {
        var now = DateTimeOffset.UtcNow;
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = Audience,
            NotBefore = now.UtcDateTime,
            Expires = now.AddHours(1).UtcDateTime,
            Subject = new ClaimsIdentity([
                new Claim(ClaimTypes.Name, "ioc-api-test"),
                new Claim(ClaimTypes.Role, nameof(UserRole.User))
            ]),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)),
                SecurityAlgorithms.HmacSha256)
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    private sealed class IocApiFactory : WebApplicationFactory<Program>
    {
        private readonly Dictionary<string, string?> _previousEnvironment;

        public IocApiFactory(string connectionString)
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
                ["ADMIN__USERNAME"] = "ioc-api-admin",
                ["ADMIN__PASSWORD"] = "Admin-test-password!",
                ["LOGGING__MINIMUMLEVEL"] = "Information"
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
                services.AddControllers().AddApplicationPart(typeof(ConflictTestController).Assembly));
        }

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

[ApiController]
[AllowAnonymous]
[Route("api/test/conflict")]
public sealed class ConflictTestController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => throw new ApiConflictException("fetch_conflict", "The fetch is already running.");
}

[ApiController]
[AllowAnonymous]
[Route("api/test/boom")]
public sealed class BoomTestController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => throw new InvalidOperationException("secret connection string should not leak");
}

[ApiController]
[Route("api/admin/ioc-test")]
public sealed class AdminOnlyIocTestController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok();
}
