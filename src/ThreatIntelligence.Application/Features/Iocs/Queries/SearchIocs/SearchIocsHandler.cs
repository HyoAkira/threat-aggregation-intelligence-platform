using MediatR;
using ThreatIntelligence.Application.Abstractions.Persistence;
using ThreatIntelligence.Application.Normalization;

namespace ThreatIntelligence.Application.Features.Iocs.Queries.SearchIocs;

public sealed class SearchIocsHandler(
    IIocClassifier classifier,
    IIocQueryReader reader) : IRequestHandler<SearchIocsQuery, PagedResult<IocGroupResult>>
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public async Task<PagedResult<IocGroupResult>> Handle(
        SearchIocsQuery request,
        CancellationToken cancellationToken)
    {
        var classification = classifier.Classify(request.Query ?? string.Empty);
        if (!classification.IsSuccess)
        {
            throw new IocQueryValidationException(
                IocErrorCodes.InvalidInput,
                classification.Error!.Message);
        }

        var provider = NormalizeProvider(request.Provider);

        var page = request.Page ?? 1;
        var pageSize = request.PageSize ?? DefaultPageSize;
        if (page < 1)
        {
            throw new IocQueryValidationException(
                IocErrorCodes.InvalidInput,
                "The page must be at least 1.");
        }

        if (pageSize is < 1 or > MaxPageSize)
        {
            throw new IocQueryValidationException(
                IocErrorCodes.InvalidInput,
                $"The pageSize must be between 1 and {MaxPageSize}.");
        }

        var identity = classification.OrThrow();
        var result = await reader.SearchGroupsAsync(
            identity,
            new IocSourceFilter(request.Tag, provider),
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

    private static string? NormalizeProvider(string? provider)
    {
        if (string.IsNullOrWhiteSpace(provider))
        {
            return null;
        }

        var trimmed = provider.Trim();
        if (trimmed.Length > 255)
        {
            throw new IocQueryValidationException(
                IocErrorCodes.InvalidInput,
                "The provider filter is too long.");
        }

        return trimmed;
    }
}
