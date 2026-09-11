using MediatR;

namespace ThreatIntelligence.Application.Features.Iocs.Queries.BrowseIocs;

/// <summary>
/// Filtered grouped browse. Raw string filters are validated by the handler so invalid enum or
/// ordering values map to the shared 400 problem detail.
/// </summary>
public sealed record BrowseIocsQuery(
    string? Type = null,
    int? Provider = null,
    string? Tag = null,
    DateTime? FirstSeenFrom = null,
    DateTime? FirstSeenTo = null,
    DateTime? LastSeenFrom = null,
    DateTime? LastSeenTo = null,
    string? OrderBy = null,
    string? Dir = null,
    int? Page = null,
    int? PageSize = null) : IRequest<PagedResult<IocGroupResult>>;
