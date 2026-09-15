using MediatR;
using ThreatIntelligence.Application.Abstractions.Persistence;

namespace ThreatIntelligence.Application.Features.Iocs.Queries.BrowseIocs;

public sealed class BrowseIocsHandler(IIocQueryReader reader)
    : IRequestHandler<BrowseIocsQuery, PagedResult<IocGroupResult>>
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public async Task<PagedResult<IocGroupResult>> Handle(
        BrowseIocsQuery request,
        CancellationToken cancellationToken)
    {
        var criteria = BuildCriteria(request);
        return await reader.BrowseAsync(criteria, cancellationToken);
    }

    private static IocBrowseCriteria BuildCriteria(BrowseIocsQuery request)
    {
        var type = ParseType(request.Type);
        var provider = NormalizeProvider(request.Provider);
        var orderBy = ParseOrderBy(request.OrderBy);
        var descending = ParseDirection(request.Dir);
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

        return new IocBrowseCriteria(
            type,
            provider,
            string.IsNullOrWhiteSpace(request.Tag) ? null : request.Tag.Trim().ToLowerInvariant(),
            request.FirstSeenFrom,
            request.FirstSeenTo,
            request.LastSeenFrom,
            request.LastSeenTo,
            orderBy,
            descending,
            page,
            pageSize);
    }

    private static Domain.Enums.IocType? ParseType(string? type)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            return null;
        }

        if (IocTypeToken.TryParse(type, out var parsed))
        {
            return parsed;
        }

        throw new IocQueryValidationException(
            IocErrorCodes.InvalidInput,
            "The type filter must be one of ipv4, ipv6, domain, or url.");
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

    private static IocBrowseOrderBy ParseOrderBy(string? orderBy)
    {
        if (string.IsNullOrWhiteSpace(orderBy))
        {
            return IocBrowseOrderBy.LastSeenUtc;
        }

        return orderBy.Trim().ToLowerInvariant() switch
        {
            "lastseenutc" => IocBrowseOrderBy.LastSeenUtc,
            "firstseenutc" => IocBrowseOrderBy.FirstSeenUtc,
            "ioctype" => IocBrowseOrderBy.IocType,
            "iocvalue" => IocBrowseOrderBy.IocValue,
            _ => throw new IocQueryValidationException(
                IocErrorCodes.InvalidInput,
                "The orderBy value must be one of lastSeenUtc, firstSeenUtc, iocType, or iocValue.")
        };
    }

    private static bool ParseDirection(string? dir)
    {
        if (string.IsNullOrWhiteSpace(dir))
        {
            return true;
        }

        return dir.Trim().ToLowerInvariant() switch
        {
            "desc" => true,
            "asc" => false,
            _ => throw new IocQueryValidationException(
                IocErrorCodes.InvalidInput,
                "The dir value must be 'asc' or 'desc'.")
        };
    }
}
