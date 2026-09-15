using MediatR;

namespace ThreatIntelligence.Application.Features.Iocs.Queries.SearchIocs;

/// <summary>
/// Combined IOC search. <paramref name="Query"/> is auto-classified and normalized by the handler;
/// domain inputs also find URL-host and parent-domain matches.
/// </summary>
public sealed record SearchIocsQuery(
    string Query,
    string? Tag = null,
    string? Provider = null,
    int? Page = null,
    int? PageSize = null) : IRequest<PagedResult<IocGroupResult>>;
