using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog.Core;
using Serilog.Events;
using ThreatIntelligence.Infrastructure.Persistence;

namespace ThreatIntelligence.Infrastructure.Configuration;

/// <summary>Applies the database-backed minimum log level without requiring a process restart.</summary>
public sealed class RuntimeLogLevelWatcher(
    IDbContextFactory<ThreatIntelligenceDbContext> contextFactory,
    LoggingLevelSwitch levelSwitch,
    ILogger<RuntimeLogLevelWatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RefreshAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Unable to refresh runtime log level");
            }

            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        }
    }

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var configured = await context.Settings.AsNoTracking()
            .Where(setting => setting.Key == "Logging:MinimumLevel")
            .Select(setting => setting.Value)
            .SingleOrDefaultAsync(cancellationToken);

        if (Enum.TryParse<LogEventLevel>(configured, ignoreCase: false, out var level))
        {
            levelSwitch.MinimumLevel = level;
        }
    }
}
