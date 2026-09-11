namespace ThreatIntelligence.Application.Fetches;

/// <summary>Stable event names emitted at the fetch lifecycle boundary.</summary>
public static class FetchLifecycleEventNames
{
    public const string Reservation = "reservation";
    public const string GuardRejection = "guard_rejection";
    public const string ExecutionStart = "execution_start";
    public const string Retry = "retry";
    public const string Success = "success";
    public const string TerminalFailure = "terminal_failure";
}

/// <summary>
/// One structured fetch lifecycle event. Every event carries the correlation id, job, provider,
/// mode, trigger, and event name; completion events also carry duration and counters. Reasons are
/// expected to be safe, redacted text and never contain credentials, full IOC values, or payloads.
/// </summary>
public sealed record FetchLifecycleEvent(
    string EventName,
    Guid? JobId,
    int ProviderId,
    string Mode,
    string Trigger,
    string CorrelationId,
    long? DurationMs = null,
    int? RecordsAdded = null,
    int? RecordsUpdated = null,
    string? Reason = null);

/// <summary>Sink for structured fetch lifecycle events. Implementations must never throw.</summary>
public interface IFetchLifecycleEventSink
{
    void Emit(FetchLifecycleEvent lifecycleEvent);
}

/// <summary>No-op sink used when no emitter is configured (for example in focused tests).</summary>
public sealed class NullFetchLifecycleEventSink : IFetchLifecycleEventSink
{
    public static readonly NullFetchLifecycleEventSink Instance = new();

    private NullFetchLifecycleEventSink()
    {
    }

    public void Emit(FetchLifecycleEvent lifecycleEvent)
    {
    }
}
