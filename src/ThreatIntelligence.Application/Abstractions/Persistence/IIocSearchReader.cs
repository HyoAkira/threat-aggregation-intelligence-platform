using ThreatIntelligence.Application.Features.Iocs.Queries.SearchIocs;

namespace ThreatIntelligence.Application.Abstractions.Persistence;

/// <summary>
/// Reads normalized IOC groups for query handlers. Infrastructure supplies the EF Core implementation.
/// </summary>
public interface IIocSearchReader
{
    Task<IocSearchResult?> SearchExactAsync(
        string query,
        string? tag,
        CancellationToken cancellationToken);
}
