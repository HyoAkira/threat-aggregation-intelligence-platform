using MediatR;
using ThreatIntelligence.Application.Abstractions;

namespace ThreatIntelligence.Application.Features.ThreatFox.DownloadDataset;

public class DownloadThreatFoxDatasetCommandHandler : IRequestHandler<DownloadThreatFoxDatasetCommand, int>
{
    private readonly IThreatFoxApiService _service;

    public DownloadThreatFoxDatasetCommandHandler(IThreatFoxApiService service)
    {
        _service = service;
    }

    public async Task<int> Handle(DownloadThreatFoxDatasetCommand request, CancellationToken cancellationToken)
    {
        return await _service.DownloadDatasetAsync(cancellationToken);
    }
}
