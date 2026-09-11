using MediatR;
using ThreatIntelligence.Application.Abstractions.Persistence;

namespace ThreatIntelligence.Application.Features.Iocs.Queries.GetIocDetail;

public sealed class GetIocDetailHandler(IIocQueryReader reader)
    : IRequestHandler<GetIocDetailQuery, IocGroupResult>
{
    public async Task<IocGroupResult> Handle(
        GetIocDetailQuery request,
        CancellationToken cancellationToken)
    {
        if (request.Id <= 0)
        {
            throw new IocQueryValidationException(
                IocErrorCodes.InvalidInput,
                "The IOC id must be a positive integer.");
        }

        var group = await reader.GetDetailGroupAsync(request.Id, cancellationToken);
        if (group is null)
        {
            throw new IocNotFoundException(
                IocErrorCodes.NotFound,
                $"No IOC row exists with id {request.Id}.");
        }

        return group;
    }
}
