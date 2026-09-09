using ThreatIntelligence.Domain.Enums;

namespace ThreatIntelligence.Application.ProviderRecords;

public sealed record ProviderRecordInput(
    int ProviderId,
    string? ProviderRecordId,
    string? RawIoc,
    IocType? DeclaredType,
    DateTime FirstSeenAt,
    DateTime? LastSeenAt,
    string? ProviderDetailsLink,
    IReadOnlyCollection<string?>? Tags,
    IReadOnlyDictionary<string, object?>? Metadata,
    IReadOnlyDictionary<string, object?>? Payload);
