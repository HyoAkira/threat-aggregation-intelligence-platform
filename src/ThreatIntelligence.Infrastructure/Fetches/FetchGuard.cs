using System.Data;
using Microsoft.EntityFrameworkCore;
using ThreatIntelligence.Application.Fetches;
using ThreatIntelligence.Domain.Entities;
using ThreatIntelligence.Domain.Enums;
using ThreatIntelligence.Infrastructure.Persistence;

namespace ThreatIntelligence.Infrastructure.Fetches;

public sealed class FetchGuard(
    IDbContextFactory<ThreatIntelligenceDbContext> contextFactory,
    TimeProvider timeProvider) : IFetchGuard
{
    public async Task<FetchReservation> ReserveAsync(
        int providerId,
        FetchTrigger trigger,
        FetchMode mode,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        // Locking the provider row serializes all reservations for that provider, including scheduler ticks.
        var provider = await context.Providers
            .FromSqlInterpolated($"SELECT * FROM providers WITH (UPDLOCK, HOLDLOCK) WHERE id = {providerId}")
            .SingleOrDefaultAsync(cancellationToken);
        var rejection = await FetchGuardEvaluator.GetRejectionAsync(
            context, provider, null, mode, timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
        if (rejection is not null)
        {
            return FetchReservation.Reject(rejection.Value);
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var jobId = Guid.NewGuid();
        context.FetchHistory.Add(new FetchHistory(jobId, providerId, trigger, mode, FetchStatus.Queued, now));
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return FetchReservation.Allow(jobId);
    }

    public async Task<bool> CanExecuteAsync(FetchJob job, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var history = await context.FetchHistory.SingleOrDefaultAsync(h => h.JobId == job.JobId, cancellationToken);
        if (history is null || history.Status is FetchStatus.Success or FetchStatus.Failed)
        {
            return false;
        }

        var provider = await context.Providers.SingleOrDefaultAsync(p => p.Id == job.ProviderId, cancellationToken);
        return await FetchGuardEvaluator.GetRejectionAsync(
            context, provider, job.JobId, job.Mode, timeProvider.GetUtcNow().UtcDateTime, cancellationToken) is null;
    }
}
