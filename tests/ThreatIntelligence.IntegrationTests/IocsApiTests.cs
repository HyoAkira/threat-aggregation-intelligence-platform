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
    public async Task Anonymous_request_can_browse_and_search_without_a_token()
    {
        using var browseRequest = new HttpRequestMessage(HttpMethod.Get, "/api/iocs");
        browseRequest.Headers.Add("X-Correlation-ID", "corr-anon");
        using var browse = await _client.SendAsync(browseRequest);

        Assert.Equal(HttpStatusCode.OK, browse.StatusCode);
        Assert.Equal("corr-anon", browse.Headers.GetValues("X-Correlation-ID").Single());
        var envelope = (await browse.Content.ReadFromJsonAsync<JsonObject>())!;
        var items = envelope["items"]!.AsArray();
        Assert.Equal(2, items.Count);
        Assert.All(items, item => Assert.True((int)item!["id"]! > 0));

        using var search = await _client.GetAsync("/api/iocs/search?q=1.2.3.4");
        Assert.Equal(HttpStatusCode.OK, search.StatusCode);
        var searchEnvelope = (await search.Content.ReadFromJsonAsync<JsonObject>())!;
        var group = searchEnvelope["items"]!.AsArray().Single()!.AsObject();
        Assert.Equal("ipv4", (string)group["iocType"]!);
        Assert.True((int)group["id"]! > 0);
    }

    [Fact]
    public async Task Tag_catalog_is_anonymous_bounded_and_usage_ordered()
    {
        await using (var context = fixture.CreateContext())
        {
            await IocQueryTestData.AddTagAsync(context, _ipv4.Id, " BotNet ");
            await IocQueryTestData.AddTagAsync(context, _url.Id, "botnet");
            await IocQueryTestData.AddTagAsync(context, _url.Id, "alpha");
            await context.SaveChangesAsync();
        }

        using var response = await _client.GetAsync("/api/iocs/tags?limit=2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = (await response.Content.ReadFromJsonAsync<JsonArray>())!;
        Assert.Equal(2, items.Count);
        Assert.Equal("botnet", (string)items[0]!["name"]!);
        Assert.Equal(2, (int)items[0]!["usageCount"]!);
        Assert.Equal("alpha", (string)items[1]!["name"]!);
        Assert.Equal(1, (int)items[1]!["usageCount"]!);
    }

    [Fact]
    public async Task Tag_catalog_normalizes_prefix_and_rejects_invalid_limits()
    {
        await using (var context = fixture.CreateContext())
        {
            await IocQueryTestData.AddTagAsync(context, _ipv4.Id, "Phishing");
            await context.SaveChangesAsync();
        }

        using var filtered = await _client.GetAsync("/api/iocs/tags?q=%20PHI%20&limit=10");
        Assert.Equal(HttpStatusCode.OK, filtered.StatusCode);
        var items = (await filtered.Content.ReadFromJsonAsync<JsonArray>())!;
        Assert.Single(items);
        Assert.Equal("phishing", (string)items[0]!["name"]!);

        using var missing = await _client.GetAsync("/api/iocs/tags");
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        using var oversized = await _client.GetAsync("/api/iocs/tags?limit=101");
        Assert.Equal(HttpStatusCode.BadRequest, oversized.StatusCode);
    }

    [Fact]
    public async Task Search_groups_sources_across_providers()
    {
        using var response = await GetAuthorized("/api/iocs/search?q=1.2.3.4");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var envelope = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        var group = envelope["items"]!.AsArray().Single()!.AsObject();
        Assert.Equal("ipv4", (string)group["iocType"]!);
        Assert.Equal(2, group["sources"]!.AsArray().Count);
    }

    [Fact]
    public async Task Search_domain_returns_url_host_matches()
    {
        using var response = await GetAuthorized("/api/iocs/search?q=evil.example");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var envelope = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal(1, (int)envelope["totalCount"]!);
        var group = envelope["items"]!.AsArray().Single()!.AsObject();
        Assert.Equal("https://evil.example/x", (string)group["iocValue"]!);
        Assert.Equal("evil.example", (string)group["urlHost"]!);
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
        Assert.Equal("application/problem+json", absent.Content.Headers.ContentType?.MediaType);
        var absentProblem = (await absent.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal("ioc_not_found", (string)absentProblem["code"]!);
        Assert.Equal((string)absentProblem["correlationId"]!, absent.Headers.GetValues("X-Correlation-ID").Single());
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
    public async Task Relations_support_malware_and_target_for_ip_groups()
    {
        int seedId;
        await using (var context = fixture.CreateContext())
        {
            var seed = await IocQueryTestData.InsertIocAsync(
                context, 1, "api-rel-meta", "44.44.44.44", IocType.Ipv4,
                malware: "Emotet", target: "Amazon");
            seedId = seed.Id;
            await IocQueryTestData.InsertIocAsync(
                context, 1, "api-rel-meta-2", "55.55.55.55", IocType.Ipv4,
                malware: "emotet", target: "Contoso");
        }

        using var malware = await GetAuthorized($"/api/iocs/{seedId}/relations?relation=malware");
        Assert.Equal(HttpStatusCode.OK, malware.StatusCode);
        var malwareEnvelope = (await malware.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal(1, (int)malwareEnvelope["totalCount"]!);
        Assert.Equal("55.55.55.55", (string)malwareEnvelope["items"]!.AsArray()[0]!["iocValue"]!);

        using var target = await GetAuthorized($"/api/iocs/{seedId}/relations?relation=target");
        Assert.Equal(HttpStatusCode.OK, target.StatusCode);
        var targetEnvelope = (await target.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal(0, (int)targetEnvelope["totalCount"]!);
    }

    [Fact]
    public async Task Search_and_browse_accept_malware_and_target_filters()
    {
        await using (var context = fixture.CreateContext())
        {
            await IocQueryTestData.InsertIocAsync(
                context, 1, "api-meta-1", "9.9.9.9", IocType.Ipv4, malware: "Emotet", target: "Amazon");
            await IocQueryTestData.InsertIocAsync(
                context, 2, "api-meta-2", "9.9.9.9", IocType.Ipv4, malware: "TrickBot", target: "Contoso");
            await IocQueryTestData.InsertIocAsync(
                context, 1, "api-meta-3", "8.8.8.8", IocType.Ipv4, malware: "Emotet", target: "Amazon");
        }

        using var search = await GetAuthorized("/api/iocs/search?q=9.9.9.9&malware=emotet");
        Assert.Equal(HttpStatusCode.OK, search.StatusCode);
        var searchEnvelope = (await search.Content.ReadFromJsonAsync<JsonObject>())!;
        var searchGroup = searchEnvelope["items"]!.AsArray().Single()!.AsObject();
        var searchSources = searchGroup["sources"]!.AsArray();
        Assert.Single(searchSources);
        Assert.Equal("Emotet", (string)searchSources[0]!["malware"]!);

        using var browse = await GetAuthorized("/api/iocs?target=AMAZON");
        Assert.Equal(HttpStatusCode.OK, browse.StatusCode);
        var browseEnvelope = (await browse.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal(2, (int)browseEnvelope["totalCount"]!);
    }

    [Fact]
    public async Task Search_and_browse_reject_over_limit_metadata_filters()
    {
        var longMalware = new string('a', 201);
        using var searchMalware = await GetAuthorized($"/api/iocs/search?q=1.2.3.4&malware={longMalware}");
        Assert.Equal(HttpStatusCode.BadRequest, searchMalware.StatusCode);
        var problem = (await searchMalware.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal("invalid_input", (string)problem["code"]!);

        var longTarget = new string('a', 501);
        using var browseTarget = await GetAuthorized($"/api/iocs?target={longTarget}");
        Assert.Equal(HttpStatusCode.BadRequest, browseTarget.StatusCode);
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

    [Fact]
    public async Task Bulk_search_is_anonymous_and_returns_ordered_mixed_statuses()
    {
        using var response = await _client.PostAsJsonAsync(
            "/api/iocs/search/bulk",
            new { queries = new[] { "1.2.3.4", "absent.example", "not an ioc" } });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal(3, (int)body["totalCount"]!);
        Assert.Equal(1, (int)body["matchedCount"]!);
        Assert.Equal(1, (int)body["notFoundCount"]!);
        Assert.Equal(1, (int)body["invalidCount"]!);

        var items = body["items"]!.AsArray();
        Assert.Equal("1.2.3.4", (string)items[0]!["input"]!);
        Assert.Equal("matched", (string)items[0]!["status"]!);
        Assert.Equal("absent.example", (string)items[1]!["input"]!);
        Assert.Equal("not_found", (string)items[1]!["status"]!);
        Assert.Equal("not an ioc", (string)items[2]!["input"]!);
        Assert.Equal("invalid", (string)items[2]!["status"]!);
        Assert.Equal("ipv4", (string)items[0]!["groups"]!.AsArray()[0]!["iocType"]!);
        Assert.Equal(2, items[0]!["groups"]!.AsArray()[0]!["sources"]!.AsArray().Count);
    }

    [Fact]
    public async Task Bulk_search_ignores_blank_lines_between_values()
    {
        using var response = await _client.PostAsJsonAsync(
            "/api/iocs/search/bulk",
            new { queries = new[] { "  1.2.3.4  ", "", "   " } });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal(1, (int)body["totalCount"]!);
        var item = body["items"]!.AsArray().Single()!;
        Assert.Equal("1.2.3.4", (string)item["input"]!);
        Assert.Equal("matched", (string)item["status"]!);
    }

    [Fact]
    public async Task Bulk_search_returns_domain_related_url_groups()
    {
        using var response = await _client.PostAsJsonAsync(
            "/api/iocs/search/bulk",
            new { queries = new[] { "evil.example" } });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        var item = body["items"]!.AsArray().Single()!;
        Assert.Equal("matched", (string)item["status"]!);
        var group = item["groups"]!.AsArray().Single()!;
        Assert.Equal("https://evil.example/x", (string)group["iocValue"]!);
        Assert.Equal("evil.example", (string)group["urlHost"]!);
    }

    [Fact]
    public async Task Bulk_search_applies_provider_filter_to_each_value()
    {
        using var response = await _client.PostAsJsonAsync(
            "/api/iocs/search/bulk",
            new { queries = new[] { "1.2.3.4", "https://evil.example/x" }, provider = "2" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        var items = body["items"]!.AsArray();
        Assert.Equal("matched", (string)items[0]!["status"]!);
        Assert.Equal("PhishTank", (string)items[0]!["groups"]!.AsArray()[0]!["sources"]!.AsArray()[0]!["provider"]!);
        Assert.Equal("not_found", (string)items[1]!["status"]!);
    }

    [Fact]
    public async Task Bulk_search_rejects_valid_limits_that_are_out_of_bounds()
    {
        using var tooMany = await _client.PostAsJsonAsync(
            "/api/iocs/search/bulk",
            new { queries = Enumerable.Range(0, 101).Select(index => $"host{index}.example").ToArray() });
        Assert.Equal(HttpStatusCode.BadRequest, tooMany.StatusCode);
        var tooManyProblem = (await tooMany.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal("invalid_input", (string)tooManyProblem["code"]!);

        using var oversized = await _client.PostAsJsonAsync(
            "/api/iocs/search/bulk",
            new { queries = new[] { new string('a', 2049) } });
        Assert.Equal(HttpStatusCode.BadRequest, oversized.StatusCode);

        using var badPage = await _client.PostAsJsonAsync(
            "/api/iocs/search/bulk",
            new { queries = new[] { "1.2.3.4" }, page = 0 });
        Assert.Equal(HttpStatusCode.BadRequest, badPage.StatusCode);

        using var badPageSize = await _client.PostAsJsonAsync(
            "/api/iocs/search/bulk",
            new { queries = new[] { "1.2.3.4" }, pageSize = 101 });
        Assert.Equal(HttpStatusCode.BadRequest, badPageSize.StatusCode);

        using var missing = await _client.PostAsJsonAsync(
            "/api/iocs/search/bulk",
            new { tag = "botnet" });
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
    }

    [Fact]
    public async Task Suggestions_return_bounded_prefix_matches_and_reject_invalid_input()
    {
        using var matching = await _client.GetAsync("/api/iocs/suggestions?q=%201.2%20");
        Assert.Equal(HttpStatusCode.OK, matching.StatusCode);
        var items = (await matching.Content.ReadFromJsonAsync<JsonArray>())!;
        var suggestion = Assert.Single(items)!.AsObject();
        Assert.Equal("1.2.3.4", (string)suggestion["value"]!);
        Assert.Equal("ipv4", (string)suggestion["iocType"]!);

        using var none = await _client.GetAsync("/api/iocs/suggestions?q=zzz");
        Assert.Equal(HttpStatusCode.OK, none.StatusCode);
        Assert.Empty((await none.Content.ReadFromJsonAsync<JsonArray>())!);

        using var blank = await _client.GetAsync("/api/iocs/suggestions");
        Assert.Equal(HttpStatusCode.BadRequest, blank.StatusCode);

        using var tooSmall = await _client.GetAsync("/api/iocs/suggestions?q=1&limit=0");
        Assert.Equal(HttpStatusCode.BadRequest, tooSmall.StatusCode);

        using var tooLarge = await _client.GetAsync("/api/iocs/suggestions?q=1&limit=21");
        Assert.Equal(HttpStatusCode.BadRequest, tooLarge.StatusCode);
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
