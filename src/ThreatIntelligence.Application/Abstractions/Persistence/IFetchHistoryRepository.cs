using ThreatIntelligence.Domain.Entities;

namespace ThreatIntelligence.Application.Abstractions.Persistence;

/// <summary>
/// Persists and reads fetch-history rows independently of the IOC unit of work, so a failed
/// IOC batch still records its Failed history row.
/// </summary>
public interface IFetchHistoryRepository
{
    Task AddAsync(FetchHistory history, CancellationToken cancellationToken);

    Task UpdateAsync(FetchHistory history, CancellationToken cancellationToken);

    Task<FetchHistory?> GetByJobIdAsync(Guid jobId, CancellationToken cancellationToken);
}