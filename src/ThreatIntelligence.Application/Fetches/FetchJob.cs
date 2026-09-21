using ThreatIntelligence.Domain.Enums;
using System.Text.Json.Serialization;

namespace ThreatIntelligence.Application.Fetches;

/// <summary>
/// Version 1 of the durable message that represents one persisted fetch-history row.
/// </summary>
public sealed record FetchJob
{
    public FetchJob(
        Guid jobId,
        int providerId,
        FetchTrigger trigger,
        FetchMode mode,
        int attempt,
        string? correlationId = null)
    {
        if (jobId == Guid.Empty)
        {
            throw new ArgumentException("JobId must not be empty.", nameof(jobId));
        }

        if (providerId < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(providerId), "ProviderId must be positive.");
        }

        if (!Enum.IsDefined(trigger))
        {
            throw new ArgumentOutOfRangeException(nameof(trigger), "Trigger must be defined.");
        }

        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode), "Mode must be defined.");
        }

        if (attempt < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(attempt), "Attempt must be positive.");
        }

        JobId = jobId;
        ProviderId = providerId;
        Trigger = trigger;
        Mode = mode;
        Attempt = attempt;
        CorrelationId = string.IsNullOrWhiteSpace(correlationId) ? jobId.ToString("N") : correlationId;
    }

    public Guid JobId { get; }
    public int ProviderId { get; }
    [JsonConverter(typeof(JsonStringEnumConverter<FetchTrigger>))]
    public FetchTrigger Trigger { get; }

    [JsonConverter(typeof(JsonStringEnumConverter<FetchMode>))]
    public FetchMode Mode { get; }
    public int Attempt { get; }

    /// <summary>
    /// Correlation id threaded from the triggering API request (or generated for scheduled jobs) so
    /// API, broker, and Worker lifecycle events for one job share a trace. Defaults to the job id.
    /// </summary>
    public string CorrelationId { get; }
}
