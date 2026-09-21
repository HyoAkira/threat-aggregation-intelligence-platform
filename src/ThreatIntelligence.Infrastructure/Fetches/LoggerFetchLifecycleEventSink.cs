using Microsoft.Extensions.Logging;
using ThreatIntelligence.Application.Fetches;
using ThreatIntelligence.Infrastructure.Admin;

namespace ThreatIntelligence.Infrastructure.Fetches;

/// <summary>
/// Writes lifecycle events as one structured log record per event. The reason is redacted first so
/// a credential-bearing URL or token in an error message never reaches the log. Only identifiers,
/// tokens, durations, and counters are emitted — never payloads or full IOC values.
/// </summary>
public sealed class LoggerFetchLifecycleEventSink(ILogger<LoggerFetchLifecycleEventSink> logger)
    : IFetchLifecycleEventSink
{
    public void Emit(FetchLifecycleEvent lifecycleEvent)
    {
        logger.LogInformation(
            "Fetch lifecycle {FetchEvent} CorrelationId={CorrelationId} JobId={JobId} ProviderId={ProviderId} Mode={Mode} Trigger={Trigger} DurationMs={DurationMs} RecordsAdded={RecordsAdded} RecordsUpdated={RecordsUpdated} Reason={Reason}",
            lifecycleEvent.EventName,
            lifecycleEvent.CorrelationId,
            lifecycleEvent.JobId,
            lifecycleEvent.ProviderId,
            lifecycleEvent.Mode,
            lifecycleEvent.Trigger,
            lifecycleEvent.DurationMs,
            lifecycleEvent.RecordsAdded,
            lifecycleEvent.RecordsUpdated,
            LogRedactor.Redact(lifecycleEvent.Reason));
    }
}
