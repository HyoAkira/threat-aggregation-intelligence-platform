namespace ThreatIntelligence.Application.Abstractions;

public interface IPhishTankApiService
{
    Task<int> DownloadDatasetAsync(CancellationToken cancellationToken = default);
}
