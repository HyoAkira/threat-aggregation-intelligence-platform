using MediatR;
using ThreatIntelligence.Application.Abstractions.Persistence;
using ThreatIntelligence.Application.Normalization;

namespace ThreatIntelligence.Application.Features.Iocs.Queries.SearchIocs;

public sealed class SearchIocsHandler(
    IIocClassifier classifier,
    IIocQueryReader reader) : IRequestHandler<SearchIocsQuery, PagedResult<IocGroupResult>>
{
    public const int DefaultPageSize = IocSearchEvaluator.DefaultPageSize;
    public const int MaxPageSize = IocSearchEvaluator.MaxPageSize;

    public async Task<PagedResult<IocGroupResult>> Handle(
        SearchIocsQuery request,
        CancellationToken cancellationToken)
    {
        var identity = IocSearchEvaluator.RequireClassification(classifier, request.Query);
        var provider = IocSearchEvaluator.NormalizeProvider(request.Provider);
        var malware = IocSearchEvaluator.NormalizeMalware(request.Malware);
        var target = IocSearchEvaluator.NormalizeTarget(request.Target);
        var (page, pageSize) = IocSearchEvaluator.ResolvePageBounds(request.Page, request.PageSize);

        var result = await reader.SearchGroupsAsync(
            identity,
            new IocSourceFilter(request.Tag, provider, malware, target),
            page,
            pageSize,
            cancellationToken);

        if (result.TotalCount == 0)
        {
            throw new IocNotFoundException(
                IocErrorCodes.NotFound,
                $"No {IocTypeToken.ToToken(identity.Type)} IOC matches '{identity.IocValue}'.");
        }

        return result;
    }
}
