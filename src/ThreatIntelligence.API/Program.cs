using System.Text;
using MassTransit;
using Serilog.Core;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Serilog;
using ThreatIntelligence.Application;
using ThreatIntelligence.API.Authorization;
using ThreatIntelligence.Infrastructure.Configuration;
using ThreatIntelligence.Infrastructure;
using ThreatIntelligence.Infrastructure.Persistence;

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

builder.Services.AddControllers(options => options.Conventions.Add(new AdminControllerConvention()));
builder.Services.AddSingleton(logLevelSwitch);
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
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
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = signingKey,
        ValidateIssuer = true,
        ValidIssuer = jwt.Issuer,
        ValidateAudience = true,
        ValidAudience = jwt.Audience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromMinutes(2),
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        NameClaimType = System.Security.Claims.ClaimTypes.Name,
        RoleClaimType = System.Security.Claims.ClaimTypes.Role
    });
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
    options.AddPolicy("Admin", policy => policy.RequireRole("Admin"));
});
var app = builder.Build();

app.Use((httpContext, next) =>
{
    var correlationId = httpContext.Request.Headers.TryGetValue("X-Correlation-ID", out var header)
        && !string.IsNullOrWhiteSpace(header)
        ? header.ToString()
        : httpContext.TraceIdentifier;

    httpContext.Response.Headers["X-Correlation-ID"] = correlationId;
    httpContext.Items["CorrelationId"] = correlationId;
    return next();
});

app.UseSerilogRequestLogging(options =>
{
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        diagnosticContext.Set("CorrelationId", httpContext.Items["CorrelationId"]);
});

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference().AllowAnonymous();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
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
