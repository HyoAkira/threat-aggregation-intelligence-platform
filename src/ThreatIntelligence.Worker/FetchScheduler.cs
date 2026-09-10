using ThreatIntelligence.Infrastructure.Fetches;
using ThreatIntelligence.Infrastructure.Persistence;

namespace ThreatIntelligence.Worker;

public sealed class FetchScheduler(
    IServiceScopeFactory scopeFactory,
    FetchSchedulerService schedulerService,
    ILogger<FetchScheduler> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using (var initializationScope = scopeFactory.CreateAsyncScope())
        {
            var initializer = initializationScope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
            await initializer.InitializeAsync(stoppingToken);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await schedulerService.TickAsync(stoppingToken);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Fetch scheduler tick failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        }
    }
}
