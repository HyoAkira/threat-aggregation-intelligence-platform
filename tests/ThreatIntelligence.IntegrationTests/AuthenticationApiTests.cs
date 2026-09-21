using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using ThreatIntelligence.API.Errors;
using ThreatIntelligence.Domain.Entities;
using ThreatIntelligence.Domain.Enums;
using ThreatIntelligence.Infrastructure.Authentication;
using ThreatIntelligence.Infrastructure.Configuration;

namespace ThreatIntelligence.IntegrationTests;

[Collection("sqlserver")]
public sealed class AuthenticationApiTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private const string AdminUsername = "auth-admin";
    private const string AdminPassword = "Admin-test-password!";
    private const string UserUsername = "auth-user";
    private const string UserPassword = "User-test-password!";
    private const string Issuer = "threat-intelligence-tests";
    private const string Audience = "threat-intelligence-api-tests";
    private static readonly string PrivateKey = TestJwtCredentials.PrivateKey;

    private AuthenticationApiFactory _factory = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await fixture.ResetDataAsync();
        _factory = new AuthenticationApiFactory(fixture.ConnectionString);
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        // Starting the host runs the real migration/provider/admin bootstrap against SQL Server.
        using var startupResponse = await _client.GetAsync("/health/live");
        startupResponse.EnsureSuccessStatusCode();

        await using var context = fixture.CreateContext();
        var hasher = new BcryptPasswordHasher(new PasswordHashOptions(4));
        context.Users.Add(new User(UserUsername, hasher.HashPassword(UserPassword), UserRole.User, DateTime.UtcNow));
        await context.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Login_health_and_role_authorization_follow_the_api_contract()
    {
        using var healthRequest = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        healthRequest.Headers.Add("X-Correlation-ID", "auth-integration-test");
        using var health = await _client.SendAsync(healthRequest);
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        Assert.Equal("auth-integration-test", health.Headers.GetValues("X-Correlation-ID").Single());

        using var anonymousBusinessRequest = new HttpRequestMessage(HttpMethod.Get, "/api/test/protected");
        anonymousBusinessRequest.Headers.Add("X-Correlation-ID", "anon-401");
        using var anonymousBusiness = await _client.SendAsync(anonymousBusinessRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousBusiness.StatusCode);
        Assert.Equal("anon-401", anonymousBusiness.Headers.GetValues("X-Correlation-ID").Single());
        Assert.Equal("application/problem+json", anonymousBusiness.Content.Headers.ContentType?.MediaType);
        var anonymousProblem = (await anonymousBusiness.Content.ReadFromJsonAsync<ApiProblemDetails>())!;
        Assert.Equal("urn:threat-intelligence:problem:unauthenticated", anonymousProblem.Type);
        Assert.Equal("unauthenticated", anonymousProblem.Code);
        Assert.Equal("anon-401", anonymousProblem.CorrelationId);
        Assert.Equal(401, anonymousProblem.Status);

        using var unknownResponse = await PostLoginAsync("missing-user", "wrong-password", "login-401");
        using var wrongPasswordResponse = await PostLoginAsync(AdminUsername, "wrong-password");
        Assert.Equal(HttpStatusCode.Unauthorized, unknownResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, wrongPasswordResponse.StatusCode);
        Assert.Equal("login-401", unknownResponse.Headers.GetValues("X-Correlation-ID").Single());
        Assert.Equal("application/problem+json", unknownResponse.Content.Headers.ContentType?.MediaType);
        var unknownProblem = (await unknownResponse.Content.ReadFromJsonAsync<ApiProblemDetails>())!;
        var wrongPasswordProblem = (await wrongPasswordResponse.Content.ReadFromJsonAsync<ApiProblemDetails>())!;
        Assert.Equal(unknownProblem.Status, wrongPasswordProblem.Status);
        Assert.Equal(unknownProblem.Title, wrongPasswordProblem.Title);
        Assert.Equal(unknownProblem.Type, wrongPasswordProblem.Type);
        Assert.Equal(401, unknownProblem.Status);
        Assert.Equal("unauthenticated", unknownProblem.Code);
        Assert.Equal("urn:threat-intelligence:problem:unauthenticated", unknownProblem.Type);
        Assert.Equal("login-401", unknownProblem.CorrelationId);
        var invalidBodies = await unknownResponse.Content.ReadAsStringAsync()
            + await wrongPasswordResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("missing-user", invalidBodies, StringComparison.Ordinal);
        Assert.DoesNotContain(AdminUsername, invalidBodies, StringComparison.Ordinal);
        Assert.DoesNotContain("wrong-password", invalidBodies, StringComparison.Ordinal);

        var userLogin = await LoginAsync(UserUsername, UserPassword);
        using var userAdminRequest = AuthorizedGet("/api/admin/test", userLogin.Token);
        using var userAdminResponse = await _client.SendAsync(userAdminRequest);
        Assert.Equal(HttpStatusCode.Forbidden, userAdminResponse.StatusCode);

        var adminLogin = await LoginAsync(AdminUsername, AdminPassword);
        using var adminRequest = AuthorizedGet("/api/admin/test", adminLogin.Token);
        using var adminResponse = await _client.SendAsync(adminRequest);
        Assert.Equal(HttpStatusCode.OK, adminResponse.StatusCode);

        var jwt = new JsonWebTokenHandler().ReadJsonWebToken(adminLogin.Token);
        Assert.Equal(SecurityAlgorithms.RsaSha256, jwt.Alg);
        Assert.Equal(Issuer, jwt.Issuer);
        Assert.Contains(Audience, jwt.Audiences);
        Assert.Contains(jwt.Claims, claim => claim.Type == ClaimTypes.Name && claim.Value == AdminUsername);
        Assert.Contains(jwt.Claims, claim => claim.Type == ClaimTypes.Role && claim.Value == nameof(UserRole.Admin));
        Assert.Equal(TimeSpan.FromHours(8), jwt.ValidTo - jwt.ValidFrom);
        Assert.Equal(jwt.ValidTo, adminLogin.ExpiresAt.UtcDateTime, TimeSpan.FromSeconds(1));

        var renderedLogs = _factory.LogSink.RenderedEvents;
        Assert.DoesNotContain(AdminPassword, renderedLogs, StringComparison.Ordinal);
        Assert.DoesNotContain(UserPassword, renderedLogs, StringComparison.Ordinal);
        Assert.DoesNotContain(adminLogin.Token, renderedLogs, StringComparison.Ordinal);
        Assert.DoesNotContain(userLogin.Token, renderedLogs, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Hardened_validation_rejects_invalid_tokens()
    {
        var validLogin = await LoginAsync(AdminUsername, AdminPassword);
        var now = DateTimeOffset.UtcNow;

        var invalidTokens = new[]
        {
            CreateToken(SecurityAlgorithms.HmacSha256, Issuer, Audience, now.AddHours(-2), now.AddHours(-1)),
            TamperSignature(validLogin.Token),
            CreateToken(SecurityAlgorithms.HmacSha384, Issuer, Audience, now, now.AddHours(1)),
            CreateToken(SecurityAlgorithms.HmacSha256, "wrong-issuer", Audience, now, now.AddHours(1)),
            CreateToken(SecurityAlgorithms.HmacSha256, Issuer, "wrong-audience", now, now.AddHours(1))
        };

        foreach (var token in invalidTokens)
        {
            using var request = AuthorizedGet("/api/admin/test", token);
            using var response = await _client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    private async Task<LoginResponse> LoginAsync(string username, string password)
    {
        using var response = await PostLoginAsync(username, password);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<LoginResponse>())!;
    }

    private Task<HttpResponseMessage> PostLoginAsync(string username, string password, string? correlationId = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new { username, password })
        };
        if (correlationId is not null)
        {
            request.Headers.Add("X-Correlation-ID", correlationId);
        }

        return _client.SendAsync(request);
    }

    private static HttpRequestMessage AuthorizedGet(string path, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private static string CreateToken(
        string algorithm,
        string issuer,
        string audience,
        DateTimeOffset notBefore,
        DateTimeOffset expires)
    {
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            NotBefore = notBefore.UtcDateTime,
            Expires = expires.UtcDateTime,
            Subject = new ClaimsIdentity([
                new Claim(ClaimTypes.Name, AdminUsername),
                new Claim(ClaimTypes.Role, nameof(UserRole.Admin))
            ]),
            SigningCredentials = TestJwtCredentials.Create(algorithm)
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    private static string TamperSignature(string token)
    {
        var parts = token.Split('.');
        var signature = parts[2].ToCharArray();
        signature[0] = signature[0] == 'A' ? 'B' : 'A';
        parts[2] = new string(signature);
        return string.Join('.', parts);
    }

    private sealed record LoginResponse(string Token, string Username, string Role, DateTimeOffset ExpiresAt);

    private sealed class AuthenticationApiFactory : WebApplicationFactory<Program>
    {
        private readonly Dictionary<string, string?> _previousEnvironment;

        public AuthenticationApiFactory(string connectionString)
        {
            var environment = new Dictionary<string, string?>
            {
                ["DATABASE__CONNECTIONSTRING"] = connectionString,
                ["RABBITMQ__HOST"] = "localhost",
                ["RABBITMQ__PORT"] = "5672",
                ["RABBITMQ__USER"] = "guest",
                ["RABBITMQ__PASSWORD"] = "guest",
                ["RABBITMQ__EXCHANGE"] = "test-exchange",
                ["JWT__PRIVATEKEYFILE"] = TestJwtCredentials.PrivateKeyFile,
                ["JWT__ISSUER"] = Issuer,
                ["JWT__AUDIENCE"] = Audience,
                ["JWT__EXPIRYMINUTES"] = "480",
                ["AUTH__PASSWORDWORKFACTOR"] = "4",
                ["THREATFOX__APIKEY"] = "test-api-key",
                ["ADMIN__USERNAME"] = AdminUsername,
                ["ADMIN__PASSWORD"] = AdminPassword,
                ["LOGGING__MINIMUMLEVEL"] = "Information"
            };

            _previousEnvironment = environment.Keys.ToDictionary(key => key, Environment.GetEnvironmentVariable);
            foreach (var (key, value) in environment)
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }

        public CapturingLogSink LogSink { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
                services.AddControllers().AddApplicationPart(typeof(AdminTestController).Assembly));
        }

        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.UseSerilog((_, _, logger) => logger
                .MinimumLevel.Verbose()
                .WriteTo.Sink(LogSink));
            return base.CreateHost(builder);
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

    private sealed class CapturingLogSink : ILogEventSink
    {
        private readonly ConcurrentQueue<LogEvent> _events = new();

        public string RenderedEvents => string.Join(
            Environment.NewLine,
            _events.Select(logEvent => $"{logEvent.RenderMessage()} {logEvent.Exception} {string.Join(' ', logEvent.Properties)}"));

        public void Emit(LogEvent logEvent) => _events.Enqueue(logEvent);
    }
}

[ApiController]
[Route("api/test/protected")]
public sealed class ProtectedTestController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok();
}

[ApiController]
[Route("api/admin/test")]
public sealed class AdminTestController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok();
}
