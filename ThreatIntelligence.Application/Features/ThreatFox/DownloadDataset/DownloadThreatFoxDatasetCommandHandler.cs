using MediatR;
using ThreatIntelligence.Application.Abstractions;
using ThreatIntelligence.Application.DTOs;

namespace ThreatIntelligence.Application.Features.ThreatFox.DownloadDataset;

public class DownloadThreatFoxDatasetCommandHandler : IRequestHandler<DownloadThreatFoxDatasetCommand, DownloadDatasetResponse>
{
    private readonly IThreatFoxApiService _service;

    public DownloadThreatFoxDatasetCommandHandler(IThreatFoxApiService service)
    {
        _service = service;
    }

    public async Task<DownloadDatasetResponse> Handle(DownloadThreatFoxDatasetCommand request, CancellationToken cancellationToken)
    {
        return await _service.DownloadDatasetAsync(cancellationToken);
    }
}