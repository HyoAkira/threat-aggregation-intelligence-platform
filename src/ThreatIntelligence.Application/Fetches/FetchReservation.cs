using ThreatIntelligence.Domain.Enums;

namespace ThreatIntelligence.Application.Fetches;

public sealed record FetchReservation(Guid? JobId, FetchReservationRejection? Rejection, FetchMode? Mode)
{
    public bool IsAllowed => JobId.HasValue;

    public static FetchReservation Allow(Guid jobId, FetchMode mode) => new(jobId, null, mode);

    public static FetchReservation Reject(FetchReservationRejection rejection) => new(null, rejection, null);
}

public enum FetchReservationRejection
{
    ProviderNotFound,
    UnsupportedMode,
    Disabled,
    ActiveFetch,
    Cooldown,
    Obsolete
}

public interface IFetchGuard
{
    /// <summary>
    /// Reserves an allowed logical fetch. A null <paramref name="mode"/> resolves to the provider's
    /// persisted default mode; the resolved mode is returned on an allowed reservation.
    /// </summary>
    Task<FetchReservation> ReserveAsync(
        int providerId,
        FetchTrigger trigger,
        FetchMode? mode,
        CancellationToken cancellationToken);

    Task<bool> CanExecuteAsync(FetchJob job, CancellationToken cancellationToken);
}

public interface IFetchJobPublisher
{
    Task PublishAsync(FetchJob job, CancellationToken cancellationToken);
}

public interface IFetchLifecycle
{
    /// <summary>
    /// Atomically rechecks the consume-time guards and claims the logical job (Queued → Running).
    /// Returns false for an obsolete duplicate; a retry/resume of the same job is allowed.
    /// </summary>
    Task<bool> TryStartAsync(FetchJob job, bool isRetry, CancellationToken cancellationToken);

    Task SucceedAsync(FetchJob job, int recordsAdded, int recordsUpdated, CancellationToken cancellationToken);

    Task FailAsync(FetchJob job, string errorDetail, CancellationToken cancellationToken);
}

public interface IProviderFetchExecutor
{
    Task<FetchExecutionResult> ExecuteAsync(FetchJob job, CancellationToken cancellationToken);
}

public sealed record FetchExecutionResult(int RecordsAdded, int RecordsUpdated);
