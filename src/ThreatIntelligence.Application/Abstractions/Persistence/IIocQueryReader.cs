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
    /// <summary>Returns the public provider-name catalog used by browse filters.</summary>
    Task<IReadOnlyList<string>> GetProviderNamesAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

    /// <summary>Returns a bounded, usage-ordered catalog of normalized IOC tags.</summary>
    Task<IReadOnlyList<IocTagCatalogItem>> GetTagCatalogAsync(
        string? query,
        int limit,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<IocTagCatalogItem>>(Array.Empty<IocTagCatalogItem>());

    /// <summary>Returns distinct normalized malware/target values with provider-row usage counts.</summary>
    Task<IReadOnlyList<IocTagCatalogItem>> GetMetadataCatalogAsync(
        IocRelationKind kind, string? query, int limit, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<IocTagCatalogItem>>(Array.Empty<IocTagCatalogItem>());

    /// <summary>
    /// Resolves groups for a normalized indicator and narrows their sources by the optional filter.
    /// IP and URL inputs match their exact normalized identity. Domain inputs additionally match
    /// rows whose URL host or parent domain equals the normalized domain.
    /// </summary>
    Task<PagedResult<IocGroupResult>> SearchGroupsAsync(
        NormalizationResult identity,
        IocSourceFilter filter,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    /// <summary>
    /// Resolves groups for several normalized indicators in one bounded candidate query, reusing the
    /// same identity semantics as <see cref="SearchGroupsAsync"/> (exact IP/URL and domain relations,
    /// explicit-port behavior, provider/tag narrowing, deterministic ordering, and shared paging).
    /// The result maps every requested identity to its page; a requested identity with no filtered
    /// groups maps to an empty page and is the caller's not-found signal.
    /// </summary>
    Task<IReadOnlyDictionary<NormalizationResult, PagedResult<IocGroupResult>>> SearchBatchAsync(
        IReadOnlyList<NormalizationResult> identities,
        IocSourceFilter filter,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    /// <summary>
    /// Returns a bounded, deterministically ordered catalog of unique normalized IOC values whose
    /// value begins with the case-insensitive prefix. Provider-row duplicates collapse to one entry.
    /// </summary>
    Task<IReadOnlyList<IocSuggestionItem>> GetIocSuggestionsAsync(
        string prefix,
        int limit,
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
    /// Returns one deterministic page of groups related by the stored host columns or by the
    /// malware/target metadata. Returns null when the id does not resolve; throws
    /// <see cref="IocRelationNotSupportedException"/> for host relations on IP groups.
    /// </summary>
    Task<PagedResult<IocGroupResult>?> GetRelatedAsync(
        int id,
        IocRelationKind relation,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
}
