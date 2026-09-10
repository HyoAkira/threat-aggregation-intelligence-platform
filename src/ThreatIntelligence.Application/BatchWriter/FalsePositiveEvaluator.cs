namespace ThreatIntelligence.Application.BatchWriter;

/// <summary>
/// Query-time calculation of a provider IOC row's false-positive state as a 180-day staleness
/// rule using the shared UTC clock. The flag is computed, never persisted.
/// </summary>
public sealed class FalsePositiveEvaluator(TimeProvider timeProvider)
{
    public static readonly TimeSpan StalenessWindow = TimeSpan.FromDays(180);

    /// <summary>
    /// Returns true only when <paramref name="updatedAtUtc"/> is strictly older than exactly
    /// 180 days before the current UTC time. A row whose <c>updatedAt</c> is exactly on the
    /// boundary is not false positive.
    /// </summary>
    public bool IsFalsePositive(DateTime updatedAtUtc)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        return updatedAtUtc < now - StalenessWindow;
    }
}