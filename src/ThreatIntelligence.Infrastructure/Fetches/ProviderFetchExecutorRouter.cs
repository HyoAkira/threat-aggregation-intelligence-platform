using ThreatIntelligence.Application.Fetches;

namespace ThreatIntelligence.Infrastructure.Fetches;

public sealed class ProviderFetchExecutorRouter(
    IReadOnlyDictionary<int, IProviderFetchExecutor> executors,
    IProviderFetchExecutor fallback) : IProviderFetchExecutor
{
    public Task<FetchExecutionResult> ExecuteAsync(FetchJob job, CancellationToken cancellationToken)
    {
        if (executors.TryGetValue(job.ProviderId, out var executor))
        {
            return executor.ExecuteAsync(job, cancellationToken);
        }

        return fallback.ExecuteAsync(job, cancellationToken);
    }
}
