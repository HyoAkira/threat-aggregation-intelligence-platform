using MediatR;

namespace ThreatIntelligence.Application.Features.ThreatFox.GetRecentIoCs;

public record GetRecentThreatFoxIoCsCommand : IRequest<int>;
