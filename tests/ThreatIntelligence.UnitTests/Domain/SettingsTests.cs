using ThreatIntelligence.Domain.Entities;

namespace ThreatIntelligence.UnitTests.Domain;

public class SettingsTests
{
    private static readonly DateTime Utc = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Valid_setting_is_constructed()
    {
        var setting = new Settings("Logging:MinimumLevel", "Information", Utc);
        Assert.Equal("Logging:MinimumLevel", setting.Key);
        Assert.Equal("Information", setting.Value);
    }

    [Fact]
    public void Blank_key_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => new Settings("  ", "value", Utc));
    }

    [Fact]
    public void Overlength_key_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => new Settings(new string('k', 101), "value", Utc));
    }

    [Fact]
    public void Blank_value_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => new Settings("key", "", Utc));
    }

    [Fact]
    public void Non_utc_updatedAt_is_rejected()
    {
        var local = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Local);
        Assert.Throws<ArgumentException>(() => new Settings("key", "value", local));
    }
}
