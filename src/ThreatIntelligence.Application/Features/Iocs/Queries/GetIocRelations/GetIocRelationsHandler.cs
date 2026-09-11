using MediatR;
using ThreatIntelligence.Application.Abstractions.Persistence;

namespace ThreatIntelligence.Application.Features.Iocs.Queries.GetIocRelations;

public sealed class GetIocRelationsHandler(IIocQueryReader reader)
    : IRequestHandler<GetIocRelationsQuery, PagedResult<IocGroupResult>>
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public async Task<PagedResult<IocGroupResult>> Handle(
        GetIocRelationsQuery request,
        CancellationToken cancellationToken)
    {
        if (request.Id <= 0)
        {
            throw new IocQueryValidationException(
                IocErrorCodes.InvalidInput,
                "The IOC id must be a positive integer.");
        }

        var relation = ParseRelation(request.Relation);
        var page = request.Page ?? 1;
        var pageSize = request.PageSize ?? DefaultPageSize;
        ValidatePage(page, pageSize);

        var result = await reader.GetRelatedAsync(
            request.Id,
            relation,
            page,
            pageSize,
            cancellationToken);

        if (result is null)
        {
            throw new IocNotFoundException(
                IocErrorCodes.NotFound,
                $"No IOC row exists with id {request.Id}.");
        }

        return result;
    }

    private static IocRelationKind ParseRelation(string? relation)
    {
        return relation?.Trim().ToLowerInvariant() switch
        {
            "urlhost" => IocRelationKind.UrlHost,
            "parentdomain" => IocRelationKind.ParentDomain,
            _ => throw new IocQueryValidationException(
                IocErrorCodes.InvalidInput,
                "The relation must be 'urlHost' or 'parentDomain'.")
        };
    }

    private static void ValidatePage(int page, int pageSize)
    {
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
    }
}
