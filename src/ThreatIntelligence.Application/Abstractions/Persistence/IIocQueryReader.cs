using ThreatIntelligence.Application.Features.Iocs.Queries;
using ThreatIntelligence.Application.Normalization;

namespace ThreatIntelligence.Application.Abstractions.Persistence;

/// <summary>
/// Reads fully assembled, source-attributed IOC groups for the query handlers. The Application
/// layer classifies and normalizes input into a canonical <see cref="NormalizationResult"/>; the
/// reader consumes canonical keys only and never re-implements classification. Infrastructure
/// supplies the EF Core implementation.
/// </summary>
public interface IIocQueryReader
{
    /// <summary>
    /// Resolves the exact group for a normalized indicator and narrows its sources by the optional
    /// filter. Returns null when no row matches the indicator or the filter removes every source.
    /// </summary>
    Task<IocGroupResult?> SearchGroupAsync(
        NormalizationResult identity,
        IocSourceFilter filter,
        CancellationToken cancellationToken);

    /// <summary>
    /// Resolves one row id to its full group across providers and ports. Returns null for an unknown id.
    /// </summary>
    Task<IocGroupResult?> GetDetailGroupAsync(int id, CancellationToken cancellationToken);

    /// <summary>
    /// Returns one deterministic page of filtered groups plus the distinct total.
    /// </summary>
    Task<PagedResult<IocGroupResult>> BrowseAsync(
        IocBrowseCriteria criteria,
        CancellationToken cancellationToken);

    /// <summary>
    /// Returns one deterministic page of groups related by exact <c>urlHost</c> or <c>parentDomain</c>.
    /// Returns null when the id does not resolve; throws <see cref="IocRelationNotSupportedException"/>
    /// for IP groups.
    /// </summary>
    Task<PagedResult<IocGroupResult>?> GetRelatedAsync(
        int id,
        IocRelationKind relation,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
}
