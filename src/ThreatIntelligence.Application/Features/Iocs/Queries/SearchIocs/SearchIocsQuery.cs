using MediatR;

namespace ThreatIntelligence.Application.Features.Iocs.Queries.SearchIocs;

/// <summary>
/// Combined exact IOC search. <paramref name="Query"/> is auto-classified and normalized by the
/// handler; the optional tag/provider filters narrow a matched group's sources without changing the
/// result shape.
/// </summary>
public sealed record SearchIocsQuery(
    string Query,
    string? Tag = null,
    int? ProviderId = null) : IRequest<IocGroupResult>;
