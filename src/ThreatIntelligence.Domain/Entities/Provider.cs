using ThreatIntelligence.Domain.Common;
using ThreatIntelligence.Domain.Enums;

namespace ThreatIntelligence.Domain.Entities;

public class Provider
{
    private Provider()
    {
    }

    public Provider(
        string name,
        string baseUrl,
        int intervalMinutes,
        bool isEnabled = true,
        FetchStatus? lastFetchStatus = null,
        DateTime? lastFetchAt = null,
        DateTime? nextFetchTime = null)
    {
        Name = Guard.Required(name, 255, nameof(name));
        BaseUrl = Guard.Required(baseUrl, 255, nameof(baseUrl));
        if (intervalMinutes < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(intervalMinutes), intervalMinutes, "Interval must be at least one minute.");
        }

        IntervalMinutes = intervalMinutes;
        IsEnabled = isEnabled;
        LastFetchStatus = lastFetchStatus is { } status
            ? Guard.Defined(status, nameof(lastFetchStatus))
            : null;
        LastFetchAt = Guard.UtcOrNull(lastFetchAt, nameof(lastFetchAt));
        NextFetchTime = Guard.UtcOrNull(nextFetchTime, nameof(nextFetchTime));
    }

    public int Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string BaseUrl { get; private set; } = string.Empty;
    public bool IsEnabled { get; private set; }
    public int IntervalMinutes { get; private set; }
    public FetchStatus? LastFetchStatus { get; private set; }
    public DateTime? LastFetchAt { get; private set; }
    public DateTime? NextFetchTime { get; private set; }

    public void SetEnabled(bool isEnabled) => IsEnabled = isEnabled;

    public void SetIntervalMinutes(int intervalMinutes)
    {
        if (intervalMinutes < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(intervalMinutes), intervalMinutes, "Interval must be at least one minute.");
        }

        IntervalMinutes = intervalMinutes;
        if (LastFetchAt.HasValue)
        {
            NextFetchTime = LastFetchAt.Value.AddMinutes(intervalMinutes);
        }
    }

    public void MarkFetchRunning() => LastFetchStatus = FetchStatus.Running;

    public void MarkFetchSucceeded(DateTime completedAtUtc, bool updateNextFetchTime = true)
    {
        LastFetchStatus = FetchStatus.Success;
        LastFetchAt = Guard.Utc(completedAtUtc, nameof(completedAtUtc));
        if (updateNextFetchTime)
        {
            NextFetchTime = LastFetchAt.Value.AddMinutes(IntervalMinutes);
        }
    }

    public void MarkFetchFailed() => LastFetchStatus = FetchStatus.Failed;

    public void SetNextFetchTime(DateTime nextFetchTimeUtc) =>
        NextFetchTime = Guard.Utc(nextFetchTimeUtc, nameof(nextFetchTimeUtc));
}
