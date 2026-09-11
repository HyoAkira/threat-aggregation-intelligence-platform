using ThreatIntelligence.Domain.Entities;

namespace ThreatIntelligence.UnitTests.Domain;

public class IoCTagTests
{
    [Fact]
    public void Valid_ioctag_is_constructed()
    {
        var link = new IoCTag(1, 2);
        Assert.Equal(1, link.IocId);
        Assert.Equal(2, link.TagId);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(1, 0)]
    [InlineData(1, -1)]
    public void Non_positive_identity_is_rejected(int iocId, int tagId)
    {
        Assert.Throws<ArgumentException>(() => new IoCTag(iocId, tagId));
    }
}
