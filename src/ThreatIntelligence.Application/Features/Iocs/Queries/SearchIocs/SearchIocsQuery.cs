using MediatR;

namespace ThreatIntelligence.Application.Features.Iocs.Queries.SearchIocs;

public sealed record SearchIocsQuery(string Query, string? Tag = null) : IRequest<IocSearchResult?>;
