using System.Text.Json;

namespace ThreatIntelligence.Application.Features.Iocs.Queries;

/// <summary>
/// One provider row's evidence inside an IOC group. Provider-specific fields stay attributed to the
/// owning source; a field that is inapplicable to the provider stays null so the global
/// ignore-on-null serialization policy omits it, while stored <c>false</c>, <c>0</c>, and empty
/// values remain non-null and are therefore retained. <see cref="IsFalsePositive"/> is computed at
/// query time from the row's <c>updatedAt</c> and is never persisted.
/// </summary>
public sealed record IocSourceResult(
    string Provider,
    string ProviderRecordId,
    string? ProviderDetailsLink,
    int? Port,
    DateTime FirstSeenUtc,
    DateTime? LastSeenUtc,
    string? ThreatType,
    string? Malware,
    string? MalwareAlias,
    string? MalwarePrintable,
    bool? IsCompromised,
    string? Reporter,
    bool? Anonymous,
    bool? IsVerified,
    string? Target,
    JsonElement? PhishTankDetails,
    bool? PhishTankOnline,
    byte? ThreatFoxConfidenceLevel,
    IReadOnlyList<string> Tags,
    bool IsFalsePositive);
