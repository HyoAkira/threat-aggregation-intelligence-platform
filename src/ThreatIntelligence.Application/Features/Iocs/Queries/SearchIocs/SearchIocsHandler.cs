using MediatR;
using ThreatIntelligence.Application.Abstractions.Persistence;
using ThreatIntelligence.Application.Normalization;

namespace ThreatIntelligence.Application.Features.Iocs.Queries.SearchIocs;

public sealed class SearchIocsHandler(
    IIocClassifier classifier,
    IIocQueryReader reader) : IRequestHandler<SearchIocsQuery, IocGroupResult>
{
    public async Task<IocGroupResult> Handle(
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

        if (request.ProviderId is <= 0)
        {
            throw new IocQueryValidationException(
                IocErrorCodes.InvalidInput,
                "The provider filter must be a positive provider id.");
        }

        var identity = classification.OrThrow();
        var group = await reader.SearchGroupAsync(
            identity,
            new IocSourceFilter(request.Tag, request.ProviderId),
            cancellationToken);

        if (group is null)
        {
            throw new IocNotFoundException(
                IocErrorCodes.NotFound,
                $"No {IocTypeToken.ToToken(identity.Type)} IOC matches '{identity.IocValue}'.");
        }

        return group;
    }
}
