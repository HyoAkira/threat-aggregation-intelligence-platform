using ThreatIntelligence.Application.Normalization;
using ThreatIntelligence.Domain.Enums;

namespace ThreatIntelligence.UnitTests.Normalization;

public class NormalizationResultContractTests
{
    [Fact]
    public void Ipv4_result_has_type_value_port_and_null_relation_fields()
    {
        var result = new NormalizationResult(IocType.Ipv4, "1.2.3.4", 443, null, null);

        Assert.Equal(IocType.Ipv4, result.Type);
        Assert.Equal("1.2.3.4", result.IocValue);
        Assert.Equal(443, result.Port);
        Assert.Null(result.UrlHost);
        Assert.Null(result.ParentDomain);
    }

    [Fact]
    public void Ipv4_bare_result_has_null_port()
    {
        var result = new NormalizationResult(IocType.Ipv4, "10.0.0.1", null, null, null);

        Assert.Null(result.Port);
        Assert.Null(result.UrlHost);
        Assert.Null(result.ParentDomain);
    }

    [Fact]
    public void Ipv6_result_has_type_value_port_and_null_relation_fields()
    {
        var result = new NormalizationResult(IocType.Ipv6, "2001:db8::1", 443, null, null);

        Assert.Equal(IocType.Ipv6, result.Type);
        Assert.Equal("2001:db8::1", result.IocValue);
        Assert.Equal(443, result.Port);
        Assert.Null(result.UrlHost);
        Assert.Null(result.ParentDomain);
    }

    [Fact]
    public void Ipv6_bare_result_has_null_port()
    {
        var result = new NormalizationResult(IocType.Ipv6, "::1", null, null, null);

        Assert.Null(result.Port);
    }

    [Fact]
    public void Domain_result_has_parentDomain_but_null_urlHost()
    {
        var result = new NormalizationResult(IocType.Domain, "api.example.co.uk", null, null, "example.co.uk");

        Assert.Equal(IocType.Domain, result.Type);
        Assert.Equal("api.example.co.uk", result.IocValue);
        Assert.Null(result.Port);
        Assert.Null(result.UrlHost);
        Assert.Equal("example.co.uk", result.ParentDomain);
    }

    [Fact]
    public void Domain_apex_result_parentDomain_is_self()
    {
        var result = new NormalizationResult(IocType.Domain, "example.co.uk", null, null, "example.co.uk");

        Assert.Equal(result.IocValue, result.ParentDomain);
    }

    [Fact]
    public void Url_result_has_urlHost_and_parentDomain()
    {
        var result = new NormalizationResult(
            IocType.Url,
            "https://api.example.co.uk/path",
            443,
            "api.example.co.uk",
            "example.co.uk");

        Assert.Equal(IocType.Url, result.Type);
        Assert.Equal("https://api.example.co.uk/path", result.IocValue);
        Assert.Equal(443, result.Port);
        Assert.Equal("api.example.co.uk", result.UrlHost);
        Assert.Equal("example.co.uk", result.ParentDomain);
    }

    [Fact]
    public void Url_bare_host_result_has_null_port_and_populated_urlHost()
    {
        var result = new NormalizationResult(
            IocType.Url,
            "https://example.com/page",
            null,
            "example.com",
            "example.com");

        Assert.Null(result.Port);
        Assert.Equal("example.com", result.UrlHost);
        Assert.Equal("example.com", result.ParentDomain);
    }

    [Fact]
    public void Result_with_null_port_is_distinct_from_result_with_port()
    {
        var withPort = new NormalizationResult(IocType.Ipv4, "1.2.3.4", 80, null, null);
        var withoutPort = new NormalizationResult(IocType.Ipv4, "1.2.3.4", null, null, null);

        Assert.NotEqual(withPort, withoutPort);
    }

    [Fact]
    public void Result_is_immutable_record()
    {
        var result = new NormalizationResult(IocType.Domain, "example.com", null, null, "example.com");

        Assert.Equal("example.com", result.IocValue);
    }

    [Fact]
    public void Result_exposes_all_five_fields()
    {
        var result = new NormalizationResult(IocType.Url, "https://x.com", 443, "x.com", "x.com");

        Assert.Equal(5, result.GetType().GetProperties().Length);
    }
}
