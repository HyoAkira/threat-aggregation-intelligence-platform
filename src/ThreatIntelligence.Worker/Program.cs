using ThreatIntelligence.Worker;
using ThreatIntelligence.Application;
using ThreatIntelligence.Infrastructure;
using ThreatIntelligence.Infrastructure.Configuration;
using Serilog;

EnvironmentConfiguration.LoadDotEnv();

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, loggerConfiguration) => loggerConfiguration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Service", "ThreatIntelligence.Worker"));

builder.Services
    .AddApplicationServices()
    .AddInfrastructureServices(builder.Configuration);
builder.Services.AddHostedService<Worker>();

var app = builder.Build();

app.MapHealthChecks("/health");
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false
});

app.Run();
