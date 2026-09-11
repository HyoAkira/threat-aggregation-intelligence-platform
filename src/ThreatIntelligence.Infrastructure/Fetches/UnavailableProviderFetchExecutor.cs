using ThreatIntelligence.Application.Fetches;

namespace ThreatIntelligence.Infrastructure.Fetches;

/// <summary>Provider adapters replace this seam in E06 and E07.</summary>
public sealed class UnavailableProviderFetchExecutor : IProviderFetchExecutor
{
    public Task<FetchExecutionResult> ExecuteAsync(FetchJob job, CancellationToken cancellationToken) =>
        throw new TerminalFetchException("No provider fetch adapter is registered for this job.");
}
