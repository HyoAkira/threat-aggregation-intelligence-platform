using MediatR;
using ThreatIntelligence.Application.DTOs;

namespace ThreatIntelligence.Application.Features.ThreatFox.GetRecentIoCs;

public record GetRecentThreatFoxIoCsCommand : IRequest<GetRecentIoCsResponse>;