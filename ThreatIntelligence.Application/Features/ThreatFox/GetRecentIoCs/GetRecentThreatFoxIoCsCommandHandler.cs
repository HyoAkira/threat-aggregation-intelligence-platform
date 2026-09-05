using MediatR;
using ThreatIntelligence.Application.Abstractions;

namespace ThreatIntelligence.Application.Features.ThreatFox.GetRecentIoCs;

public class GetRecentThreatFoxIoCsCommandHandler : IRequestHandler<GetRecentThreatFoxIoCsCommand, int>
{
    private readonly IThreatFoxApiService _service;

    public GetRecentThreatFoxIoCsCommandHandler(IThreatFoxApiService service)
    {
        _service = service;
    }

    public async Task<int> Handle(GetRecentThreatFoxIoCsCommand request, CancellationToken cancellationToken)
    {
        return await _service.RecentIoCsAsync(cancellationToken);
    }
}
