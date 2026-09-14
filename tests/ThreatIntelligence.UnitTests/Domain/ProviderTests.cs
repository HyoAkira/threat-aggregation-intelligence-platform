using ThreatIntelligence.Domain.Entities;
using ThreatIntelligence.Domain.Enums;

namespace ThreatIntelligence.UnitTests.Domain;

public class ProviderTests
{
    [Fact]
    public void Valid_provider_is_constructed()
    {
        var provider = new Provider("ThreatFox", "https://threatfox-api.abuse.ch", 5);
        Assert.Equal("ThreatFox", provider.Name);
        Assert.Equal(5, provider.IntervalMinutes);
        Assert.True(provider.IsEnabled);
    }

    [Fact]
    public void Blank_name_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => new Provider("  ", "https://x", 5));
    }

    [Fact]
    public void Blank_baseUrl_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => new Provider("ThreatFox", "", 5));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Interval_below_one_minute_is_rejected(int intervalMinutes)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new Provider("ThreatFox", "https://x", intervalMinutes));
    }

    [Fact]
    public void Undefined_lastFetchStatus_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => new Provider(
            "ThreatFox", "https://x", 5, lastFetchStatus: (FetchStatus)99));
    }

    [Fact]
    public void Non_utc_lastFetchAt_is_rejected()
    {
        var local = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Local);
        Assert.Throws<ArgumentException>(() => new Provider(
            "ThreatFox", "https://x", 5, lastFetchAt: local));
    }

    [Fact]
    public void Default_configuration_supports_recent_and_full()
    {
        var provider = new Provider("ThreatFox", "https://x", 5);

        Assert.Equal(FetchMode.Recent, provider.DefaultMode);
        Assert.True(provider.HasMode(FetchMode.Recent));
        Assert.True(provider.HasMode(FetchMode.Full));
        Assert.Equal(5, provider.MinimumIntervalMinutes);
        Assert.Null(provider.RecentCooldownMinutes);
        Assert.Null(provider.FullCooldownMinutes);
    }

    [Fact]
    public void Default_mode_outside_supported_modes_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => new Provider(
            "PhishTank",
            "https://x",
            480,
            defaultMode: FetchMode.Recent,
            supportedModes: ProviderFetchModes.Full));
    }

    [Fact]
    public void Empty_supported_modes_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => new Provider(
            "PhishTank",
            "https://x",
            480,
            defaultMode: FetchMode.Full,
            supportedModes: ProviderFetchModes.None));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Non_positive_cooldown_is_rejected(int cooldownMinutes)
    {
        Assert.Throws<ArgumentException>(() => new Provider(
            "ThreatFox",
            "https://x",
            5,
            recentCooldownMinutes: cooldownMinutes));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Non_positive_minimum_interval_is_rejected(int minimumIntervalMinutes)
    {
        Assert.Throws<ArgumentException>(() => new Provider(
            "ThreatFox",
            "https://x",
            5,
            minimumIntervalMinutes: minimumIntervalMinutes));
    }

    [Fact]
    public void CooldownFor_returns_the_configured_per_mode_cooldown()
    {
        var provider = new Provider(
            "ThreatFox",
            "https://x",
            5,
            recentCooldownMinutes: 5,
            fullCooldownMinutes: 60);

        Assert.Equal(TimeSpan.FromMinutes(5), provider.CooldownFor(FetchMode.Recent));
        Assert.Equal(TimeSpan.FromMinutes(60), provider.CooldownFor(FetchMode.Full));
    }

    [Fact]
    public void Configure_replaces_configuration_and_validates()
    {
        var provider = new Provider(
            "ThreatFox",
            "https://x",
            5,
            recentCooldownMinutes: 5,
            fullCooldownMinutes: 60);

        provider.Configure(
            FetchMode.Full,
            ProviderFetchModes.Full,
            recentCooldownMinutes: null,
            fullCooldownMinutes: 30,
            minimumIntervalMinutes: 10);

        Assert.Equal(FetchMode.Full, provider.DefaultMode);
        Assert.False(provider.HasMode(FetchMode.Recent));
        Assert.True(provider.HasMode(FetchMode.Full));
        Assert.Null(provider.RecentCooldownMinutes);
        Assert.Equal(30, provider.FullCooldownMinutes);
        Assert.Equal(10, provider.MinimumIntervalMinutes);

        Assert.Throws<ArgumentException>(() => provider.Configure(
            FetchMode.Recent,
            ProviderFetchModes.Full,
            recentCooldownMinutes: null,
            fullCooldownMinutes: 30,
            minimumIntervalMinutes: 10));
    }
}
