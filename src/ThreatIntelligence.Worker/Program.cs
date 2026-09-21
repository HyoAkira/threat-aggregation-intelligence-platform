using ThreatIntelligence.Worker;
using MassTransit;
using Serilog.Core;
using ThreatIntelligence.Application;
using ThreatIntelligence.Application.Fetches;
using ThreatIntelligence.Infrastructure;
using ThreatIntelligence.Infrastructure.Configuration;
using ThreatIntelligence.Infrastructure.Fetches;
using Serilog;

EnvironmentConfiguration.LoadDotEnv();

var builder = WebApplication.CreateBuilder(args);
var logLevelSwitch = new LoggingLevelSwitch();

builder.Host.UseSerilog((context, services, loggerConfiguration) => loggerConfiguration
    .ReadFrom.Configuration(context.Configuration)
    .MinimumLevel.ControlledBy(logLevelSwitch)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Service", "ThreatIntelligence.Worker"));

builder.Services
    .AddSingleton(logLevelSwitch)
    .AddApplicationServices()
    .AddInfrastructureServices(builder.Configuration);
builder.Services.AddHostedService<Worker>();
builder.Services.AddHostedService<FetchScheduler>();
var rabbitMq = ThreatIntelligenceOptions.BindAndValidate(builder.Configuration).RabbitMq;
builder.Services.AddMassTransit(configurator =>
{
    configurator.AddConsumer<FetchJobConsumer>();
    configurator.UsingRabbitMq((context, bus) =>
    {
        bus.Message<FetchJob>(message => message.SetEntityName(rabbitMq.Exchange));
        bus.Host(new Uri($"rabbitmq://{rabbitMq.Host}:{rabbitMq.Port}/"), host =>
        {
            host.Username(rabbitMq.User);
            host.Password(rabbitMq.Password);
        });
        bus.ReceiveEndpoint("tieto.fetch.jobs", endpoint =>
        {
            endpoint.PrefetchCount = 1;
            endpoint.ConcurrentMessageLimit = 1;
            endpoint.UseMessageRetry(retry =>
                retry.Intervals(FetchRetryPolicy.Intervals.ToArray()).Handle<TransientFetchException>());
            endpoint.ConfigureConsumer<FetchJobConsumer>(context);
        });
    });
});

var app = builder.Build();

app.MapHealthChecks("/health");
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false
});

app.Run();
