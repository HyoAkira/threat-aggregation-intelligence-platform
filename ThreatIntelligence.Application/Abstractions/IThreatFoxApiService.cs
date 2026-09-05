namespace ThreatIntelligence.Application.Abstractions;

public interface IThreatFoxApiService
{
    Task<int> DownloadDatasetAsync(CancellationToken cancellationToken = default);
    Task<int> RecentIoCsAsync(CancellationToken cancellationToken = default);
}
