using MassTransit;
using Microsoft.Extensions.Logging;
using ThreatIntelligence.Application.Fetches;

namespace ThreatIntelligence.Infrastructure.Fetches;

public sealed class FetchJobConsumer(
    IFetchLifecycle fetchLifecycle,
    IProviderFetchExecutor executor,
    ILogger<FetchJobConsumer> logger) : IConsumer<FetchJob>
{
    public async Task Consume(ConsumeContext<FetchJob> context)
    {
        var job = context.Message;
        if (!await fetchLifecycle.TryStartAsync(job, context.GetRetryAttempt() > 0, context.CancellationToken))
        {
            logger.LogInformation("Skipping obsolete fetch job {JobId} for provider {ProviderId}", job.JobId, job.ProviderId);
            return;
        }

        try
        {
            var result = await executor.ExecuteAsync(job, context.CancellationToken);
            await fetchLifecycle.SucceedAsync(job.JobId, result.RecordsAdded, result.RecordsUpdated, context.CancellationToken);
        }
        catch (Exception exception)
        {
            var classified = FetchErrorClassifier.Classify(exception);
            if (classified is TerminalFetchException)
            {
                await fetchLifecycle.FailAsync(job.JobId, classified.Message, context.CancellationToken);
                return;
            }

            if (context.GetRetryAttempt() >= 3)
            {
                await fetchLifecycle.FailAsync(job.JobId, classified.Message, context.CancellationToken);
            }

            throw classified;
        }
    }
}
