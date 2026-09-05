using MediatR;
using ThreatIntelligence.Application.Abstractions;
using ThreatIntelligence.Application.DTOs;

namespace ThreatIntelligence.Application.Features.PhishTank.DownloadDataset;

public class DownloadPhishTankDatasetCommandHandler : IRequestHandler<DownloadPhishTankDatasetCommand, DownloadDatasetResponse>
{
    private readonly IPhishTankApiService _service;

    public DownloadPhishTankDatasetCommandHandler(IPhishTankApiService service)
    {
        _service = service;
    }

    public async Task<DownloadDatasetResponse> Handle(DownloadPhishTankDatasetCommand request, CancellationToken cancellationToken)
    {
        return await _service.DownloadDatasetAsync(cancellationToken);
    }
}