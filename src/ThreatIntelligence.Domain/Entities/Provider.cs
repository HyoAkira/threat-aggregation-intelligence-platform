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
        DateTime? nextFetchTime = null,
        FetchMode defaultMode = FetchMode.Recent,
        ProviderFetchModes supportedModes = ProviderFetchModes.Recent | ProviderFetchModes.Full,
        int? recentCooldownMinutes = null,
        int? fullCooldownMinutes = null,
        int? minimumIntervalMinutes = null)
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
        Configure(
            defaultMode,
            supportedModes,
            recentCooldownMinutes,
            fullCooldownMinutes,
            minimumIntervalMinutes ?? intervalMinutes);
    }

    public int Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string BaseUrl { get; private set; } = string.Empty;
    public bool IsEnabled { get; private set; }
    public int IntervalMinutes { get; private set; }
    public FetchStatus? LastFetchStatus { get; private set; }
    public DateTime? LastFetchAt { get; private set; }
    public DateTime? NextFetchTime { get; private set; }
    public FetchMode DefaultMode { get; private set; }
    public ProviderFetchModes SupportedModes { get; private set; }
    public int? RecentCooldownMinutes { get; private set; }
    public int? FullCooldownMinutes { get; private set; }
    public int MinimumIntervalMinutes { get; private set; }

    public bool HasMode(FetchMode mode) => (SupportedModes & mode.ToProviderMode()) != ProviderFetchModes.None;

    public TimeSpan? CooldownFor(FetchMode mode) => mode switch
    {
        FetchMode.Recent => RecentCooldownMinutes is { } minutes ? TimeSpan.FromMinutes(minutes) : null,
        FetchMode.Full => FullCooldownMinutes is { } minutes ? TimeSpan.FromMinutes(minutes) : null,
        _ => null
    };

    public void Configure(
        FetchMode defaultMode,
        ProviderFetchModes supportedModes,
        int? recentCooldownMinutes,
        int? fullCooldownMinutes,
        int minimumIntervalMinutes)
    {
        if (supportedModes == ProviderFetchModes.None)
        {
            throw new ArgumentException("At least one fetch mode must be supported.", nameof(supportedModes));
        }

        if ((supportedModes & ~(ProviderFetchModes.Recent | ProviderFetchModes.Full)) != ProviderFetchModes.None)
        {
            throw new ArgumentException("supportedModes contains an undefined mode.", nameof(supportedModes));
        }

        Guard.Defined(defaultMode, nameof(defaultMode));
        if ((supportedModes & defaultMode.ToProviderMode()) == ProviderFetchModes.None)
        {
            throw new ArgumentException("The default mode must be one of the supported modes.", nameof(defaultMode));
        }

        if (recentCooldownMinutes is <= 0)
        {
            throw new ArgumentException("recentCooldownMinutes must be positive when set.", nameof(recentCooldownMinutes));
        }

        if (fullCooldownMinutes is <= 0)
        {
            throw new ArgumentException("fullCooldownMinutes must be positive when set.", nameof(fullCooldownMinutes));
        }

        if (minimumIntervalMinutes <= 0)
        {
            throw new ArgumentException("minimumIntervalMinutes must be positive.", nameof(minimumIntervalMinutes));
        }

        DefaultMode = defaultMode;
        SupportedModes = supportedModes;
        RecentCooldownMinutes = recentCooldownMinutes;
        FullCooldownMinutes = fullCooldownMinutes;
        MinimumIntervalMinutes = minimumIntervalMinutes;
    }

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
