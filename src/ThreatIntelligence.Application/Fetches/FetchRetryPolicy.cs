namespace ThreatIntelligence.Application.Fetches;

/// <summary>
/// The single source of truth for job-level transient retry timing (TI-003): after the initial
/// attempt, retry at 10 seconds, 30 seconds, and 1 minute, then dead-letter to the default
/// <c>_error</c> queue. Provider HTTP clients do not retry independently.
/// </summary>
public static class FetchRetryPolicy
{
    public static IReadOnlyList<TimeSpan> Intervals { get; } =
    [
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(1)
    ];
}
