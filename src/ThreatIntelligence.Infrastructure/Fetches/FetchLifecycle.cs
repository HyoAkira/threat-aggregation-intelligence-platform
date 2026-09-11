using System.Data;
using Microsoft.EntityFrameworkCore;
using ThreatIntelligence.Application.Fetches;
using ThreatIntelligence.Domain.Enums;
using ThreatIntelligence.Infrastructure.Admin;
using ThreatIntelligence.Infrastructure.Persistence;

namespace ThreatIntelligence.Infrastructure.Fetches;

public sealed class FetchLifecycle(
    IDbContextFactory<ThreatIntelligenceDbContext> contextFactory,
    TimeProvider timeProvider,
    IFetchLifecycleEventSink? eventSink = null) : IFetchLifecycle
{
    private readonly IFetchLifecycleEventSink _eventSink = eventSink ?? NullFetchLifecycleEventSink.Instance;

    public async Task<bool> TryStartAsync(FetchJob job, bool isRetry, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var history = await context.FetchHistory.SingleOrDefaultAsync(h => h.JobId == job.JobId, cancellationToken);
        if (history is null || history.Status is FetchStatus.Success or FetchStatus.Failed)
        {
            Emit(FetchLifecycleEventNames.GuardRejection, job, reason: "obsolete");
            return false;
        }

        // A message must match the reservation it claims, including on retries/redelivery.
        if (history.ProviderId != job.ProviderId || history.Trigger != job.Trigger || history.Mode != job.Mode)
        {
            Emit(FetchLifecycleEventNames.GuardRejection, job, reason: "reservation_mismatch");
            return false;
        }

        var provider = await context.Providers.SingleOrDefaultAsync(p => p.Id == job.ProviderId, cancellationToken);
        var rejection = await FetchGuardEvaluator.GetRejectionAsync(
            context, provider, job.JobId, job.Mode, timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
        if (rejection is not null)
        {
            Emit(FetchLifecycleEventNames.GuardRejection, job, reason: rejection.Value.ToString());
            return false;
        }

        if (isRetry)
        {
            Emit(FetchLifecycleEventNames.Retry, job, reason: $"attempt {job.Attempt}");
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
        Emit(FetchLifecycleEventNames.ExecutionStart, job);
        return true;
    }

    public Task SucceedAsync(FetchJob job, int recordsAdded, int recordsUpdated, CancellationToken cancellationToken) =>
        CompleteAsync(
            job,
            history => history.Succeed(recordsAdded, recordsUpdated, timeProvider.GetUtcNow().UtcDateTime),
            success: true,
            recordsAdded,
            recordsUpdated,
            cancellationToken);

    public Task FailAsync(FetchJob job, string errorDetail, CancellationToken cancellationToken) =>
        CompleteAsync(
            job,
            history => history.Fail(errorDetail, timeProvider.GetUtcNow().UtcDateTime),
            success: false,
            0,
            0,
            cancellationToken,
            LogRedactor.Redact(errorDetail));

    private async Task CompleteAsync(
        FetchJob job,
        Action<ThreatIntelligence.Domain.Entities.FetchHistory> transition,
        bool success,
        int recordsAdded,
        int recordsUpdated,
        CancellationToken cancellationToken,
        string? reason = null)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var history = await context.FetchHistory.SingleOrDefaultAsync(h => h.JobId == job.JobId, cancellationToken);
        if (history is null || history.Status is FetchStatus.Success or FetchStatus.Failed)
        {
            return;
        }

        var provider = await context.Providers.SingleAsync(p => p.Id == history.ProviderId, cancellationToken);
        var startedAt = history.StartedAt;
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
        var durationMs = (long)(timeProvider.GetUtcNow().UtcDateTime - startedAt).TotalMilliseconds;
        Emit(
            success ? FetchLifecycleEventNames.Success : FetchLifecycleEventNames.TerminalFailure,
            job,
            durationMs,
            recordsAdded,
            recordsUpdated,
            reason);
    }

    private void Emit(
        string eventName,
        FetchJob job,
        long? durationMs = null,
        int? recordsAdded = null,
        int? recordsUpdated = null,
        string? reason = null) =>
        _eventSink.Emit(new FetchLifecycleEvent(
            eventName,
            job.JobId,
            job.ProviderId,
            FetchTokens.Mode(job.Mode),
            FetchTokens.Trigger(job.Trigger),
            job.CorrelationId,
            durationMs,
            recordsAdded,
            recordsUpdated,
            reason));
}
