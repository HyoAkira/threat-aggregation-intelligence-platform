using ThreatIntelligence.Domain.Enums;

namespace ThreatIntelligence.Application.Features.Iocs.Queries;

/// <summary>
/// Optional source-narrowing filters applied after an exact search resolves a group. A non-null
/// <see cref="Tag"/> is normalized (trimmed and lowercased) before matching; <see cref="Malware"/>
/// and <see cref="Target"/> match the stored values trimmed and case-insensitively.
/// </summary>
public sealed record IocSourceFilter(
    string? Tag = null,
    string? Provider = null,
    string? Malware = null,
    string? Target = null);

/// <summary>Allowed aggregate sort fields for grouped browse.</summary>
public enum IocBrowseOrderBy
{
    LastSeenUtc,
    FirstSeenUtc,
    IocType,
    IocValue
}

/// <summary>Allowed relation kinds: the stored host columns and the malware/target metadata.</summary>
public enum IocRelationKind
{
    UrlHost,
    ParentDomain,
    Malware,
    Target
}

/// <summary>
/// Validated browse parameters. The Application handler rejects invalid values before the reader is
/// called, so the reader may assume <see cref="Page"/> and <see cref="PageSize"/> are within bounds.
/// </summary>
public sealed record IocBrowseCriteria(
    IocType? Type,
    string? Provider,
    string? Tag,
    DateTime? FirstSeenFrom,
    DateTime? FirstSeenTo,
    DateTime? LastSeenFrom,
    DateTime? LastSeenTo,
    IocBrowseOrderBy OrderBy,
    bool Descending,
    int Page,
    int PageSize,
    IReadOnlyList<IocType>? Types = null,
    string? Malware = null,
    string? Target = null);
