namespace ThreatIntelligence.Worker;

public sealed class Worker(ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Threat Intelligence worker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        }
    }
}
