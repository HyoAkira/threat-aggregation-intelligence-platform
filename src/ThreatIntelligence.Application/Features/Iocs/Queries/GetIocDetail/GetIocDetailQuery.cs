using MediatR;

namespace ThreatIntelligence.Application.Features.Iocs.Queries.GetIocDetail;

public sealed record GetIocDetailQuery(int Id) : IRequest<IocGroupResult>;
