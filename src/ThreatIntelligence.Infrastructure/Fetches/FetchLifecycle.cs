using System.Data;
using Microsoft.EntityFrameworkCore;
using ThreatIntelligence.Application.Fetches;
using ThreatIntelligence.Domain.Enums;
using ThreatIntelligence.Infrastructure.Persistence;

namespace ThreatIntelligence.Infrastructure.Fetches;

public sealed class FetchLifecycle(
    IDbContextFactory<ThreatIntelligenceDbContext> contextFactory,
    TimeProvider timeProvider) : IFetchLifecycle
{
    public async Task<bool> TryStartAsync(FetchJob job, bool isRetry, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var history = await context.FetchHistory.SingleOrDefaultAsync(h => h.JobId == job.JobId, cancellationToken);
        if (history is null || history.Status is FetchStatus.Success or FetchStatus.Failed)
        {
            return false;
        }

        // A message must match the reservation it claims, including on retries/redelivery.
        if (history.ProviderId != job.ProviderId || history.Trigger != job.Trigger || history.Mode != job.Mode)
        {
            return false;
        }

        var provider = await context.Providers.SingleOrDefaultAsync(p => p.Id == job.ProviderId, cancellationToken);
        var rejection = await FetchGuardEvaluator.GetRejectionAsync(
            context, provider, job.JobId, job.Mode, timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
        if (rejection is not null)
        {
            return false;
        }

        // A retry or a crash-resume reuses the already-claimed Running row, but only after
        // the current provider guards have passed.
        if (history.Status == FetchStatus.Running)
        {
            return true;
        }

        history.Start();
        provider!.MarkFetchRunning();
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public Task SucceedAsync(Guid jobId, int recordsAdded, int recordsUpdated, CancellationToken cancellationToken) =>
        CompleteAsync(jobId, history => history.Succeed(recordsAdded, recordsUpdated, timeProvider.GetUtcNow().UtcDateTime), true, cancellationToken);

    public Task FailAsync(Guid jobId, string errorDetail, CancellationToken cancellationToken) =>
        CompleteAsync(jobId, history => history.Fail(errorDetail, timeProvider.GetUtcNow().UtcDateTime), false, cancellationToken);

    private async Task CompleteAsync(Guid jobId, Action<ThreatIntelligence.Domain.Entities.FetchHistory> transition, bool success, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var history = await context.FetchHistory.SingleOrDefaultAsync(h => h.JobId == jobId, cancellationToken);
        if (history is null || history.Status is FetchStatus.Success or FetchStatus.Failed)
        {
            return;
        }

        var provider = await context.Providers.SingleAsync(p => p.Id == history.ProviderId, cancellationToken);
        transition(history);
        if (success)
        {
            provider.MarkFetchSucceeded(
                timeProvider.GetUtcNow().UtcDateTime,
                updateNextFetchTime: history.Trigger != FetchTrigger.Scheduled);
        }
        else
        {
            provider.MarkFetchFailed();
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
