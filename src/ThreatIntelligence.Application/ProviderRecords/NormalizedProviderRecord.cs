using ThreatIntelligence.Application.Normalization;

namespace ThreatIntelligence.Application.ProviderRecords;

public sealed class NormalizedProviderRecord
{
    internal NormalizedProviderRecord(
        int providerId,
        string providerRecordId,
        NormalizationResult iocIdentity,
        DateTimeOffset firstSeenUtc,
        DateTimeOffset? lastSeenUtc,
        string? providerDetailsLink,
        IReadOnlyList<string> tags,
        IReadOnlyDictionary<string, object?>? metadata,
        IReadOnlyDictionary<string, object?>? payload)
    {
        ProviderId = providerId;
        ProviderRecordId = providerRecordId;
        IocIdentity = iocIdentity;
        FirstSeenUtc = firstSeenUtc;
        LastSeenUtc = lastSeenUtc;
        ProviderDetailsLink = providerDetailsLink;
        Tags = tags;
        Metadata = metadata;
        Payload = payload;
    }

    public int ProviderId { get; }
    public string ProviderRecordId { get; }
    public NormalizationResult IocIdentity { get; }
    public DateTimeOffset FirstSeenUtc { get; }
    public DateTimeOffset? LastSeenUtc { get; }
    public string? ProviderDetailsLink { get; }
    public IReadOnlyList<string> Tags { get; }
    public IReadOnlyDictionary<string, object?>? Metadata { get; }
    public IReadOnlyDictionary<string, object?>? Payload { get; }
}
