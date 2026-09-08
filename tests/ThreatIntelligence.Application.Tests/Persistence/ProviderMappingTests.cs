using System.Text.Json;
using ThreatIntelligence.Domain.Enums;
using ThreatIntelligence.Infrastructure.Services;

namespace ThreatIntelligence.Application.Tests.Persistence;

public class ProviderMappingTests : IDisposable
{
    private readonly string _publicSuffixListPath;
    private readonly IocNormalizer _normalizer;

    public ProviderMappingTests()
    {
        _publicSuffixListPath = Path.GetTempFileName();
        File.WriteAllText(_publicSuffixListPath, "com\nco.uk\n");
        _normalizer = new IocNormalizer(_publicSuffixListPath);
    }

    [Fact]
    public void ThreatFoxMapsDomainMetadataAndNormalizedTags()
    {
        using var document = JsonDocument.Parse("""{"42":{"ioc_value":"API.Example.CO.UK.","ioc_type":"domain","first_seen_utc":"2026-09-08T10:00:00Z","last_seen_utc":"2026-09-08T11:00:00Z","threat_type":"botnet","malware":"ExampleWare","confidence_level":75,"tags":" Malware, phishing,malware "}}""");
        var service = new ThreatFoxApiService(new HttpClient(), _normalizer, null!);

        var record = Assert.Single(service.ParseJsonResponse(document));

        Assert.Equal("42", record.Ioc.ProviderRecordId);
        Assert.Equal(IocType.Domain, record.Ioc.IocType);
        Assert.Equal("api.example.co.uk", record.Ioc.IocValue);
        Assert.Equal("example.co.uk", record.Ioc.ParentDomain);
        Assert.Equal("botnet", record.Ioc.ThreatType);
        Assert.Equal("ExampleWare", record.Ioc.Malware);
        Assert.Equal((byte)75, record.Ioc.ThreatFoxConfidenceLevel);
        Assert.Equal(["malware", "phishing"], record.Tags.OrderBy(tag => tag));
    }

    [Fact]
    public void ThreatFoxMapsUrlAndIpPorts()
    {
        using var document = JsonDocument.Parse("""{"7":[{"ioc_value":"https://Portal.Example.COM:8443/login#ignored","ioc_type":"url","first_seen_utc":"2026-09-08T10:00:00Z"},{"ioc_value":"192.0.2.1:8080","ioc_type":"ipv4","first_seen_utc":"2026-09-08T10:00:00Z"}]}""");
        var service = new ThreatFoxApiService(new HttpClient(), _normalizer, null!);

        var records = service.ParseJsonResponse(document);

        Assert.Collection(records,
            url =>
            {
                Assert.Equal("https://portal.example.com:8443/login", url.Ioc.IocValue);
                Assert.Equal(8443, url.Ioc.Port);
                Assert.Equal("portal.example.com", url.Ioc.UrlHost);
            },
            ip =>
            {
                Assert.Equal(IocType.Ipv4, ip.Ioc.IocType);
                Assert.Equal("192.0.2.1", ip.Ioc.IocValue);
                Assert.Equal(8080, ip.Ioc.Port);
            });
    }

    [Fact]
    public void PhishTankMapsUrlMetadataAndCompleteDetails()
    {
        using var document = JsonDocument.Parse("""[{"phish_id":99,"url":"HTTPS://Login.Example.CO.UK:443/?next=Home#fragment","phish_detail_url":"https://phishtank.example/99","submission_time":"2026-09-08T10:00:00Z","verification_time":"2026-09-08T11:00:00Z","verified":"yes","online":"no","target":"Example","details":[{"detail_time":"2026-09-08T10:30:00Z","detail":"verified"}]}]""");
        var service = new PhishTankApiService(new HttpClient(), _normalizer, null!);

        var record = Assert.Single(service.ParseJsonArray(document));

        Assert.Equal("99", record.Ioc.ProviderRecordId);
        Assert.Equal("https://login.example.co.uk?next=Home", record.Ioc.IocValue);
        Assert.Equal(443, record.Ioc.Port);
        Assert.Equal("login.example.co.uk", record.Ioc.UrlHost);
        Assert.Equal("example.co.uk", record.Ioc.ParentDomain);
        Assert.True(record.Ioc.IsVerified);
        Assert.False(record.Ioc.PhishTankOnline);
        Assert.Equal("Example", record.Ioc.Target);
        using var details = JsonDocument.Parse(record.Ioc.PhishTankDetails!);
        Assert.Equal("verified", details.RootElement[0].GetProperty("detail").GetString());
    }

    [Fact]
    public void PhishTankRejectsInvalidRecords()
    {
        var service = new PhishTankApiService(new HttpClient(), _normalizer, null!);
        using var invalidId = JsonDocument.Parse("""[{"phish_id":0,"url":"https://example.com","submission_time":"2026-09-08T10:00:00Z"}]""");
        using var invalidDetails = JsonDocument.Parse("""[{"phish_id":1,"url":"https://example.com","submission_time":"2026-09-08T10:00:00Z","details":"not-an-array"}]""");

        Assert.Throws<InvalidDataException>(() => service.ParseJsonArray(invalidId));
        Assert.Throws<InvalidDataException>(() => service.ParseJsonArray(invalidDetails));
    }

    public void Dispose()
    {
        File.Delete(_publicSuffixListPath);
    }
}
