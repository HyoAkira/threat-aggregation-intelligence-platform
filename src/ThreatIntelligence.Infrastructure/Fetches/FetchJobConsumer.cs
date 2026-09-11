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
            await fetchLifecycle.SucceedAsync(job, result.RecordsAdded, result.RecordsUpdated, context.CancellationToken);
        }
        catch (Exception exception)
        {
            var attempt = context.GetRetryAttempt();
            var classified = FetchErrorClassifier.Classify(exception);
            if (classified is TerminalFetchException)
            {
                logger.LogError(
                    exception,
                    "Fetch job {JobId} for provider {ProviderId} mode {Mode} hit a terminal failure on attempt {Attempt}",
                    job.JobId,
                    job.ProviderId,
                    job.Mode,
                    attempt + 1);
                await fetchLifecycle.FailAsync(job, classified.Message, context.CancellationToken);
                return;
            }

            if (attempt >= 3)
            {
                logger.LogError(
                    exception,
                    "Fetch job {JobId} for provider {ProviderId} mode {Mode} exhausted retries at attempt {Attempt}",
                    job.JobId,
                    job.ProviderId,
                    job.Mode,
                    attempt);
                await fetchLifecycle.FailAsync(job, classified.Message, context.CancellationToken);
            }
            else
            {
                logger.LogWarning(
                    exception,
                    "Fetch job {JobId} for provider {ProviderId} mode {Mode} hit a transient failure on attempt {Attempt}; will retry",
                    job.JobId,
                    job.ProviderId,
                    job.Mode,
                    attempt);
            }

            throw classified;
        }
    }
}
