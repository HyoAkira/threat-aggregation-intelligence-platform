using ThreatIntelligence.Domain.Entities;

namespace ThreatIntelligence.UnitTests.Domain;

public class TagTests
{
    [Fact]
    public void Name_is_trimmed_and_lowercased()
    {
        var tag = new Tag("  BotNet  ");
        Assert.Equal("botnet", tag.Name);
    }

    [Fact]
    public void Blank_name_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => new Tag("   "));
    }

    [Fact]
    public void Null_name_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => new Tag(null!));
    }

    [Fact]
    public void Overlength_normalized_name_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => new Tag(new string('t', 101)));
    }
}
