using MediatR;
using ThreatIntelligence.Application.Abstractions;
using ThreatIntelligence.Application.DTOs;

namespace ThreatIntelligence.Application.Features.ThreatFox.GetRecentIoCs;

public class GetRecentThreatFoxIoCsCommandHandler : IRequestHandler<GetRecentThreatFoxIoCsCommand, GetRecentIoCsResponse>
{
    private readonly IThreatFoxApiService _service;

    public GetRecentThreatFoxIoCsCommandHandler(IThreatFoxApiService service)
    {
        _service = service;
    }

    public async Task<GetRecentIoCsResponse> Handle(GetRecentThreatFoxIoCsCommand request, CancellationToken cancellationToken)
    {
        return await _service.RecentIoCsAsync(cancellationToken);
    }
}