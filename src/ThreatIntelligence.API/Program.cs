using System.Security.Cryptography;
using MassTransit;
using Serilog.Core;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Serilog;
using ThreatIntelligence.Application;
using ThreatIntelligence.API.Authorization;
using ThreatIntelligence.API.Errors;
using ThreatIntelligence.API.Serialization;
using ThreatIntelligence.Infrastructure.Configuration;
using ThreatIntelligence.Infrastructure;
using ThreatIntelligence.Infrastructure.Persistence;
using ThreatIntelligence.API.Mcp;
using ModelContextProtocol.Server;
using ModelContextProtocol.AspNetCore;
using Microsoft.Extensions.Options;
using ThreatIntelligence.Application.AI;
using ThreatIntelligence.API.Chat;

EnvironmentConfiguration.LoadDotEnv();

var builder = WebApplication.CreateBuilder(args);
var logLevelSwitch = new LoggingLevelSwitch();

builder.Host.UseSerilog((context, services, loggerConfiguration) => loggerConfiguration
    .ReadFrom.Configuration(context.Configuration)
    .MinimumLevel.ControlledBy(logLevelSwitch)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Service", "ThreatIntelligence.API"));

// Add services to the container.

builder.Services.AddControllers(options => options.Conventions.Add(new AdminControllerConvention()))
    .AddJsonOptions(options => ApiJson.Configure(options.JsonSerializerOptions));
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var detail = string.Join(
            " ",
            context.ModelState
                .Where(entry => entry.Value?.Errors.Count > 0)
                .SelectMany(entry => entry.Value!.Errors.Select(error =>
                    string.IsNullOrWhiteSpace(error.ErrorMessage)
                        ? entry.Key
                        : $"{entry.Key}: {error.ErrorMessage}")));
        if (string.IsNullOrWhiteSpace(detail))
        {
            detail = "One or more request values are invalid.";
        }

        return ApiProblemFactory.Result(
            context.HttpContext,
            StatusCodes.Status400BadRequest,
            ApiProblemFactory.ValidationCode,
            "Validation failed",
            detail);
    };
});
builder.Services.AddSingleton(logLevelSwitch);
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer<BearerSecuritySchemeDocumentTransformer>();
    options.AddOperationTransformer<AnonymousSecurityOperationTransformer>();
    options.AddOperationTransformer<ApiProblemOpenApiTransformer>();
});
var jwt = ThreatIntelligenceOptions.BindAndValidate(builder.Configuration).Jwt;
builder.Services.AddApplicationServices()
                   .AddInfrastructureServices(builder.Configuration);
var rabbitMq = ThreatIntelligenceOptions.BindAndValidate(builder.Configuration).RabbitMq;
builder.Services.AddMassTransit(configurator =>
{
    configurator.UsingRabbitMq((context, bus) =>
    {
        bus.Message<ThreatIntelligence.Application.Fetches.FetchJob>(message =>
            message.SetEntityName(rabbitMq.Exchange));
        bus.Host(new Uri($"rabbitmq://{rabbitMq.Host}:{rabbitMq.Port}/"), host =>
        {
            host.Username(rabbitMq.User);
            host.Password(rabbitMq.Password);
        });
    });
});
var rsa = RSA.Create();
rsa.ImportFromPem(jwt.PrivateKey.Replace("\\n", "\n"));
var signingKey = new RsaSecurityKey(rsa);
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(2),
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
            NameClaimType = System.Security.Claims.ClaimTypes.Name,
            RoleClaimType = System.Security.Claims.ClaimTypes.Role
        };
        options.Events = new JwtBearerEvents
        {
            OnChallenge = async challengeContext =>
            {
                challengeContext.HandleResponse();
                challengeContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await ApiProblemFactory.WriteAsync(challengeContext.Response, ApiProblemFactory.Create(
                    challengeContext.HttpContext,
                    StatusCodes.Status401Unauthorized,
                    ApiProblemFactory.UnauthenticatedCode,
                    "Unauthorized",
                    "Authentication is required to access this resource."), challengeContext.HttpContext.RequestAborted);
            },
            OnForbidden = async forbiddenContext =>
            {
                forbiddenContext.Response.StatusCode = StatusCodes.Status403Forbidden;
                await ApiProblemFactory.WriteAsync(forbiddenContext.Response, ApiProblemFactory.Create(
                    forbiddenContext.HttpContext,
                    StatusCodes.Status403Forbidden,
                    ApiProblemFactory.ForbiddenCode,
                    "Forbidden",
                    "You do not have permission to perform this action."), forbiddenContext.HttpContext.RequestAborted);
            }
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
    options.AddPolicy("Admin", policy => policy.RequireRole("Admin"));
});

// Register MCP server for public IOC tools
builder.Services.AddMcpServer()
    .WithHttpTransport(options => options.Stateless = true)
    .WithToolsFromAssembly(typeof(PublicIocMcpServer).Assembly);
builder.Services.AddScoped<PublicIocMcpServer>();
builder.Services.AddScoped<IInProcessIocMcpClient, InProcessIocMcpClient>();
builder.Services.AddScoped<ThreatIntelligence.Infrastructure.AI.IocChatService>();
builder.Services.AddSingleton<ChatRateLimiter>();

// Register MCP configuration and rate limiter
builder.Services.Configure<McpConfiguration>(builder.Configuration.GetSection("Mcp"));
builder.Services.AddSingleton<McpRateLimiter>(sp =>
{
    var config = sp.GetRequiredService<IOptions<McpConfiguration>>().Value;
    return new McpRateLimiter(config);
});

var app = builder.Build();

app.Use(async (httpContext, next) =>
{
    var correlationId = httpContext.Request.Headers.TryGetValue("X-Correlation-ID", out var header)
        && !string.IsNullOrWhiteSpace(header)
        ? header.ToString()
        : httpContext.TraceIdentifier;

    httpContext.Response.Headers["X-Correlation-ID"] = correlationId;
    httpContext.Items["CorrelationId"] = correlationId;
    using (Serilog.Context.LogContext.PushProperty("CorrelationId", correlationId))
    {
        await next();
    }
});

app.UseSerilogRequestLogging();

app.UseMiddleware<ApiExceptionHandlingMiddleware>();
app.UseStatusCodePages(ApiProblemStatusCodePages.HandleAsync);
app.UseMiddleware<McpMiddleware>();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference().AllowAnonymous();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapMcp("/mcp").AllowAnonymous();
app.MapHealthChecks("/health").AllowAnonymous();
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false
}).AllowAnonymous();

await using (var scope = app.Services.CreateAsyncScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
    await initializer.InitializeAsync(app.Lifetime.ApplicationStopping);
}

app.Run();

public partial class Program;
