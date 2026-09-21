using MassTransit;
using ThreatIntelligence.Application.Fetches;

namespace ThreatIntelligence.Infrastructure.Fetches;

public sealed class MassTransitFetchJobPublisher(IPublishEndpoint publishEndpoint) : IFetchJobPublisher
{
    public Task PublishAsync(FetchJob job, CancellationToken cancellationToken) =>
        publishEndpoint.Publish(job, cancellationToken);
}
