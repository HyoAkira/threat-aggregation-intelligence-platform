using ThreatIntelligence.Domain.Entities;
using ThreatIntelligence.Domain.Enums;

namespace ThreatIntelligence.UnitTests.Domain;

public class IocTests
{
    private static readonly DateTime Utc = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly byte[] Hash = Enumerable.Repeat((byte)7, 32).ToArray();

    private static IoC Valid() => new(
        providerId: 1,
        providerRecordId: "ioc-1",
        iocValue: "1.2.3.4",
        iocValueHash: Hash,
        iocType: IocType.Ipv4,
        firstSeenUtc: Utc,
        createdAt: Utc,
        updatedAt: Utc);

    [Fact]
    public void Valid_ioc_is_constructed()
    {
        var ioc = Valid();
        Assert.Equal(1, ioc.ProviderId);
        Assert.Equal(32, ioc.IocValueHash.Length);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Non_positive_providerId_is_rejected(int providerId)
    {
        Assert.Throws<ArgumentException>(() => new IoC(
            providerId,
            "ioc-1",
            "1.2.3.4",
            Hash,
            IocType.Ipv4,
            Utc,
            Utc,
            Utc));
    }

    [Fact]
    public void Blank_providerRecordId_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => new IoC(
            1, "  ", "1.2.3.4", Hash, IocType.Ipv4, Utc, Utc, Utc));
    }

    [Fact]
    public void Overlength_providerRecordId_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => new IoC(
            1, new string('r', 101), "1.2.3.4", Hash, IocType.Ipv4, Utc, Utc, Utc));
    }

    [Fact]
    public void Blank_iocValue_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => new IoC(
            1, "ioc-1", "", Hash, IocType.Ipv4, Utc, Utc, Utc));
    }

    [Fact]
    public void Overlength_iocValue_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => new IoC(
            1, "ioc-1", new string('v', 2049), Hash, IocType.Ipv4, Utc, Utc, Utc));
    }

    [Fact]
    public void Undefined_iocType_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => new IoC(
            1, "ioc-1", "1.2.3.4", Hash, (IocType)99, Utc, Utc, Utc));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65536)]
    [InlineData(70000)]
    public void Out_of_range_port_is_rejected(int port)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new IoC(
            1, "ioc-1", "1.2.3.4", Hash, IocType.Ipv4, Utc, Utc, Utc, port: port));
    }

    [Fact]
    public void Null_port_is_allowed()
    {
        var ioc = Valid();
        Assert.Null(ioc.Port);
    }

    [Fact]
    public void Confidence_above_100_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new IoC(
            1, "ioc-1", "1.2.3.4", Hash, IocType.Ipv4, Utc, Utc, Utc, threatFoxConfidenceLevel: 150));
    }

    [Fact]
    public void Zero_confidence_is_preserved()
    {
        var ioc = new IoC(
            1, "ioc-1", "1.2.3.4", Hash, IocType.Ipv4, Utc, Utc, Utc, threatFoxConfidenceLevel: 0);
        Assert.Equal((byte)0, ioc.ThreatFoxConfidenceLevel);
    }

    [Fact]
    public void Hash_shorter_than_32_bytes_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => new IoC(
            1, "ioc-1", "1.2.3.4", new byte[31], IocType.Ipv4, Utc, Utc, Utc));
    }

    [Fact]
    public void Hash_longer_than_32_bytes_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => new IoC(
            1, "ioc-1", "1.2.3.4", new byte[33], IocType.Ipv4, Utc, Utc, Utc));
    }

    [Fact]
    public void Non_utc_firstSeenUtc_is_rejected()
    {
        var local = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Local);
        Assert.Throws<ArgumentException>(() => new IoC(
            1, "ioc-1", "1.2.3.4", Hash, IocType.Ipv4, local, Utc, Utc));
    }

    [Fact]
    public void Non_utc_createdAt_is_rejected()
    {
        var local = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Local);
        Assert.Throws<ArgumentException>(() => new IoC(
            1, "ioc-1", "1.2.3.4", Hash, IocType.Ipv4, Utc, local, Utc));
    }

    [Fact]
    public void Non_utc_updatedAt_is_rejected()
    {
        var local = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Local);
        Assert.Throws<ArgumentException>(() => new IoC(
            1, "ioc-1", "1.2.3.4", Hash, IocType.Ipv4, Utc, Utc, local));
    }
}
