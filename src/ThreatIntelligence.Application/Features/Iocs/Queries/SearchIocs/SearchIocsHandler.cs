using MediatR;
using ThreatIntelligence.Application.Abstractions.Persistence;

namespace ThreatIntelligence.Application.Features.Iocs.Queries.SearchIocs;

public sealed class SearchIocsHandler(IIocSearchReader reader)
    : IRequestHandler<SearchIocsQuery, IocSearchResult?>
{
    public Task<IocSearchResult?> Handle(
        SearchIocsQuery request,
        CancellationToken cancellationToken)
    {
        return reader.SearchExactAsync(request.Query, request.Tag, cancellationToken);
    }
}
