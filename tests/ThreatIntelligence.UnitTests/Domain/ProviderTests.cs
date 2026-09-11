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
}
