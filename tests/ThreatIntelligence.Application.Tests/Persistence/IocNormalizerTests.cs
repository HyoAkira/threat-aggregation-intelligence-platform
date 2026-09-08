using System.Security.Cryptography;
using System.Text;
using ThreatIntelligence.Domain.Enums;
using ThreatIntelligence.Infrastructure.Services;

namespace ThreatIntelligence.Application.Tests.Persistence;

public class IocNormalizerTests : IDisposable
{
    private readonly string _publicSuffixListPath;
    private readonly IocNormalizer _normalizer;

    public IocNormalizerTests()
    {
        _publicSuffixListPath = Path.GetTempFileName();
        File.WriteAllText(_publicSuffixListPath, "com\nco.uk\n");
        _normalizer = new IocNormalizer(_publicSuffixListPath);
    }

    [Fact]
    public void NormalizesUrlAndRetainsExplicitDefaultPort()
    {
        var result = _normalizer.Normalize("HTTPS://Api.Example.CO.UK:443/path?Case=Preserved#fragment", IocType.Url);

        Assert.Equal(IocType.Url, result.IocType);
        Assert.Equal("https://api.example.co.uk/path?Case=Preserved", result.IocValue);
        Assert.Equal(443, result.Port);
        Assert.Equal("api.example.co.uk", result.UrlHost);
        Assert.Equal("example.co.uk", result.ParentDomain);
    }

    [Fact]
    public void NormalizesDomainAndIpPortValues()
    {
        var domain = _normalizer.Normalize("API.Example.CO.UK.", IocType.Domain);
        var ipv4 = _normalizer.Normalize("192.0.2.5:8443");
        var ipv6 = _normalizer.Normalize("[2001:db8::1]:443");

        Assert.Equal("api.example.co.uk", domain.IocValue);
        Assert.Equal("example.co.uk", domain.ParentDomain);
        Assert.Equal(IocType.Ipv4, ipv4.IocType);
        Assert.Equal("192.0.2.5", ipv4.IocValue);
        Assert.Equal(8443, ipv4.Port);
        Assert.Equal(IocType.Ipv6, ipv6.IocType);
        Assert.Equal("2001:db8::1", ipv6.IocValue);
        Assert.Equal(443, ipv6.Port);
    }

    [Fact]
    public void RejectsInvalidValuesAndProducesStableHashes()
    {
        Assert.Throws<InvalidDataException>(() => _normalizer.Normalize("https://example.com:70000", IocType.Url));
        Assert.Throws<InvalidDataException>(() => _normalizer.Normalize("01.2.3.4", IocType.Ipv4));

        var first = _normalizer.Normalize("example.com", IocType.Domain);
        var second = _normalizer.Normalize("EXAMPLE.COM", IocType.Domain);

        Assert.Equal(first.IocValueHash, second.IocValueHash);
        Assert.Equal(SHA256.HashData(Encoding.UTF8.GetBytes("example.com")), first.IocValueHash);
    }

    public void Dispose()
    {
        File.Delete(_publicSuffixListPath);
    }
}
