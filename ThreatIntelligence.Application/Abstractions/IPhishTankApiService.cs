using ThreatIntelligence.Application.DTOs;

namespace ThreatIntelligence.Application.Abstractions;

public interface IPhishTankApiService
{
    Task<DownloadDatasetResponse> DownloadDatasetAsync(CancellationToken cancellationToken = default);
}