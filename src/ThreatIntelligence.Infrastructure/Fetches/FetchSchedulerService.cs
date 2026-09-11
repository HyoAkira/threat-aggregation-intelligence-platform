using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ThreatIntelligence.Application.Fetches;
using ThreatIntelligence.Domain.Enums;
using ThreatIntelligence.Infrastructure.Persistence;
using ThreatIntelligence.Infrastructure.ThreatFox;

namespace ThreatIntelligence.Infrastructure.Fetches;

/// <summary>
/// Publishes scheduled fetches for enabled, due providers and advances each provider's
/// <c>nextFetchTime</c> by its configured interval. Kept separate from the hosting loop so the
/// due/guard/advance rules are testable with an injected <see cref="TimeProvider"/>.
/// </summary>
public sealed class FetchSchedulerService(
    IDbContextFactory<ThreatIntelligenceDbContext> contextFactory,
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    IFetchLifecycleEventSink? eventSink = null)
{
    private readonly IFetchLifecycleEventSink _eventSink = eventSink ?? NullFetchLifecycleEventSink.Instance;

    public async Task<int> TickAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var dueProviders = await context.Providers.AsNoTracking()
            .Where(provider => provider.IsEnabled && provider.NextFetchTime != null && provider.NextFetchTime <= now)
            .Select(provider => new ScheduledProvider(provider.Id, provider.IntervalMinutes))
            .ToListAsync(cancellationToken);

        var published = 0;
        foreach (var provider in dueProviders)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var guard = scope.ServiceProvider.GetRequiredService<IFetchGuard>();
            var publisher = scope.ServiceProvider.GetRequiredService<IFetchJobPublisher>();
            var lifecycle = scope.ServiceProvider.GetRequiredService<IFetchLifecycle>();
            var mode = provider.Id == ThreatFoxProviderValidator.ThreatFoxProviderId
                ? FetchMode.Recent
                : FetchMode.Full;
            var correlationId = Guid.NewGuid().ToString("N");
            var reservation = await guard.ReserveAsync(provider.Id, FetchTrigger.Scheduled, mode, cancellationToken);
            if (!reservation.IsAllowed)
            {
                // Guarded (e.g. cooldown): defer to a later tick without a publish storm.
                Emit(
                    FetchLifecycleEventNames.GuardRejection,
                    null,
                    provider.Id,
                    mode,
                    FetchTrigger.Scheduled,
                    correlationId,
                    reservation.Rejection?.ToString());
                continue;
            }

            var job = new FetchJob(reservation.JobId!.Value, provider.Id, FetchTrigger.Scheduled, mode, 1, correlationId);
            Emit(FetchLifecycleEventNames.Reservation, job);
            try
            {
                await publisher.PublishAsync(job, cancellationToken);
                published++;
            }
            catch (Exception exception)
            {
                await lifecycle.FailAsync(job, $"Unable to publish scheduled fetch job: {exception.Message}", cancellationToken);
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

    private void Emit(
        string eventName,
        FetchJob? job,
        int providerId,
        FetchMode mode,
        FetchTrigger trigger,
        string correlationId,
        string? reason = null) =>
        _eventSink.Emit(new FetchLifecycleEvent(
            eventName,
            job?.JobId,
            providerId,
            FetchTokens.Mode(mode),
            FetchTokens.Trigger(trigger),
            job?.CorrelationId ?? correlationId,
            Reason: reason));

    private void Emit(string eventName, FetchJob job) =>
        _eventSink.Emit(new FetchLifecycleEvent(
            eventName,
            job.JobId,
            job.ProviderId,
            FetchTokens.Mode(job.Mode),
            FetchTokens.Trigger(job.Trigger),
            job.CorrelationId));

    private sealed record ScheduledProvider(int Id, int IntervalMinutes);
}
