using ThreatIntelligence.Domain.Enums;
using System.Text.Json.Serialization;

namespace ThreatIntelligence.Application.Fetches;

/// <summary>
/// Version 1 of the durable message that represents one persisted fetch-history row.
/// </summary>
public sealed record FetchJob
{
    public FetchJob(Guid jobId, int providerId, FetchTrigger trigger, FetchMode mode, int attempt)
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
    }

    public Guid JobId { get; }
    public int ProviderId { get; }
    [JsonConverter(typeof(JsonStringEnumConverter<FetchTrigger>))]
    public FetchTrigger Trigger { get; }

    [JsonConverter(typeof(JsonStringEnumConverter<FetchMode>))]
    public FetchMode Mode { get; }
    public int Attempt { get; }
}
