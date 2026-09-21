using ThreatIntelligence.Domain.Enums;

namespace ThreatIntelligence.UnitTests.Domain;

public class EnumContractTests
{
    [Theory]
    [InlineData(IocType.Ipv4, 0)]
    [InlineData(IocType.Ipv6, 1)]
    [InlineData(IocType.Domain, 2)]
    [InlineData(IocType.Url, 3)]
    public void IocType_exposes_documented_ordinal(IocType value, byte expected)
    {
        Assert.Equal(expected, (byte)value);
    }

    [Theory]
    [InlineData(FetchTrigger.Scheduled, 0)]
    [InlineData(FetchTrigger.Manual, 1)]
    public void FetchTrigger_exposes_documented_ordinal(FetchTrigger value, byte expected)
    {
        Assert.Equal(expected, (byte)value);
    }

    [Theory]
    [InlineData(FetchMode.Recent, 0)]
    [InlineData(FetchMode.Full, 1)]
    public void FetchMode_exposes_documented_ordinal(FetchMode value, byte expected)
    {
        Assert.Equal(expected, (byte)value);
    }

    [Theory]
    [InlineData(FetchStatus.Queued, 0)]
    [InlineData(FetchStatus.Running, 1)]
    [InlineData(FetchStatus.Success, 2)]
    [InlineData(FetchStatus.Failed, 3)]
    public void FetchStatus_exposes_documented_ordinal(FetchStatus value, byte expected)
    {
        Assert.Equal(expected, (byte)value);
    }

    [Theory]
    [InlineData(UserRole.User, 0)]
    [InlineData(UserRole.Admin, 1)]
    public void UserRole_exposes_documented_ordinal(UserRole value, byte expected)
    {
        Assert.Equal(expected, (byte)value);
    }
}
