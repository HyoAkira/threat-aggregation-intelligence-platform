using MediatR;
using ThreatIntelligence.Application.Abstractions;

namespace ThreatIntelligence.Application.Features.PhishTank.DownloadDataset;

public class DownloadPhishTankDatasetCommandHandler : IRequestHandler<DownloadPhishTankDatasetCommand, int>
{
    private readonly IPhishTankApiService _service;

    public DownloadPhishTankDatasetCommandHandler(IPhishTankApiService service)
    {
        _service = service;
    }

    public async Task<int> Handle(DownloadPhishTankDatasetCommand request, CancellationToken cancellationToken)
    {
        return await _service.DownloadDatasetAsync(cancellationToken);
    }
}
