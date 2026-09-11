using MediatR;

namespace ThreatIntelligence.Application.Features.Iocs.Queries.GetIocRelations;

/// <summary>
/// Opt-in, bounded relation request. <paramref name="Relation"/> must be <c>urlHost</c> or
/// <c>parentDomain</c>; page bounds follow the shared envelope (default 20, maximum 100).
/// </summary>
public sealed record GetIocRelationsQuery(
    int Id,
    string? Relation,
    int? Page = null,
    int? PageSize = null) : IRequest<PagedResult<IocGroupResult>>;
