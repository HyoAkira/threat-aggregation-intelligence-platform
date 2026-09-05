using ThreatIntelligence.Application.DTOs;

namespace ThreatIntelligence.Application.Abstractions;

public interface IThreatFoxApiService
{
    Task<DownloadDatasetResponse> DownloadDatasetAsync(CancellationToken cancellationToken = default);
    Task<GetRecentIoCsResponse> RecentIoCsAsync(CancellationToken cancellationToken = default);
}