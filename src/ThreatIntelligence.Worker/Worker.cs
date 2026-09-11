using ThreatIntelligence.Infrastructure.Persistence;

namespace ThreatIntelligence.Worker;

public sealed class Worker(
    ILogger<Worker> logger,
    IServiceScopeFactory scopeFactory) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Threat Intelligence worker started");

        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
            await initializer.InitializeAsync(stoppingToken);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        }
    }
}