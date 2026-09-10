using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ThreatIntelligence.Application.Fetches;
using ThreatIntelligence.Domain.Enums;
using ThreatIntelligence.Infrastructure.Persistence;

namespace ThreatIntelligence.Infrastructure.Fetches;

/// <summary>
/// Publishes scheduled fetches for enabled, due providers and advances each provider's
/// <c>nextFetchTime</c> by its configured interval. Kept separate from the hosting loop so the
/// due/guard/advance rules are testable with an injected <see cref="TimeProvider"/>.
/// </summary>
public sealed class FetchSchedulerService(
    IDbContextFactory<ThreatIntelligenceDbContext> contextFactory,
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider)
{
    public async Task<int> TickAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var dueProviders = await context.Providers.AsNoTracking()
            .Where(provider => provider.IsEnabled && provider.NextFetchTime != null && provider.NextFetchTime <= now)
            .Select(provider => new ScheduledProvider(provider.Id, provider.Name, provider.IntervalMinutes))
            .ToListAsync(cancellationToken);

        var published = 0;
        foreach (var provider in dueProviders)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var guard = scope.ServiceProvider.GetRequiredService<IFetchGuard>();
            var publisher = scope.ServiceProvider.GetRequiredService<IFetchJobPublisher>();
            var lifecycle = scope.ServiceProvider.GetRequiredService<IFetchLifecycle>();
            var mode = provider.Name == "ThreatFox" ? FetchMode.Recent : FetchMode.Full;
            var reservation = await guard.ReserveAsync(provider.Id, FetchTrigger.Scheduled, mode, cancellationToken);
            if (!reservation.IsAllowed)
            {
                // Guarded (e.g. cooldown): defer to a later tick without a publish storm.
                continue;
            }

            var job = new FetchJob(reservation.JobId!.Value, provider.Id, FetchTrigger.Scheduled, mode, 1);
            try
            {
                await publisher.PublishAsync(job, cancellationToken);
                published++;
            }
            catch (Exception exception)
            {
                await lifecycle.FailAsync(job.JobId, $"Unable to publish scheduled fetch job: {exception.Message}", cancellationToken);
            }

            // Advance regardless so a failed publish or run waits for the next interval.
            // Reload after publication so an interval/settings update that raced this tick is
            // not overwritten with the stale snapshot used to decide that the provider was due.
            var tracked = await context.Providers.SingleAsync(item => item.Id == provider.Id, cancellationToken);
            if (tracked.NextFetchTime <= now)
            {
                tracked.SetNextFetchTime(now.AddMinutes(tracked.IntervalMinutes));
                await context.SaveChangesAsync(cancellationToken);
            }
        }

        return published;
    }

    private sealed record ScheduledProvider(int Id, string Name, int IntervalMinutes);
}
