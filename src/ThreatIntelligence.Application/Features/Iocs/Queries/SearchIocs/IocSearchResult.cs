using ThreatIntelligence.Domain.Enums;

namespace ThreatIntelligence.Application.Features.Iocs.Queries.SearchIocs;

/// <summary>
/// One normalized IOC group with source-specific evidence retained for the API response.
/// </summary>
public sealed record IocSearchResult(
    string IocValue,
    IocType IocType,
    string? UrlHost,
    string? ParentDomain,
    DateTime FirstSeenUtc,
    DateTime? LastSeenUtc,
    IReadOnlyList<IocSourceResult> Sources);

public sealed record IocSourceResult(
    string Provider,
    string ProviderRecordId,
    string? ProviderDetailsLink,
    int? Port,
    DateTime FirstSeenUtc,
    DateTime? LastSeenUtc,
    string? ThreatType,
    string? Malware,
    byte? ThreatFoxConfidenceLevel,
    bool? IsVerified,
    string? Target,
    bool? PhishTankOnline,
    IReadOnlyList<string> Tags,
    bool IsFalsePositive);
