using ThreatIntelligence.Application.Normalization;

namespace ThreatIntelligence.UnitTests.Normalization;

public class UrlPortTests
{
    private readonly UrlNormalizer _normalizer = new(new ThreatIntelligence.Infrastructure.PublicSuffix.PublicSuffixParentDomainResolver());

    [Fact]
    public void Default_http_port_is_removed_from_value_but_kept_in_port()
    {
        var result = _normalizer.Normalize("http://example.com:80/a").OrThrow();
        Assert.Equal("http://example.com/a", result.IocValue);
        Assert.Equal(80, result.Port);
    }

    [Fact]
    public void Default_https_port_is_removed_from_value_but_kept_in_port()
    {
        var result = _normalizer.Normalize("https://example.com:443/a").OrThrow();
        Assert.Equal("https://example.com/a", result.IocValue);
        Assert.Equal(443, result.Port);
    }

    [Fact]
    public void Non_default_http_port_is_kept_in_value_and_port()
    {
        var result = _normalizer.Normalize("http://example.com:443/a").OrThrow();
        Assert.Equal("http://example.com:443/a", result.IocValue);
        Assert.Equal(443, result.Port);
    }

    [Fact]
    public void Non_default_https_port_is_kept_in_value_and_port()
    {
        var result = _normalizer.Normalize("https://example.com:8080/a").OrThrow();
        Assert.Equal("https://example.com:8080/a", result.IocValue);
        Assert.Equal(8080, result.Port);
    }

    [Fact]
    public void Boundary_http_port_65535_is_kept()
    {
        var result = _normalizer.Normalize("http://example.com:65535").OrThrow();
        Assert.Equal("http://example.com:65535", result.IocValue);
        Assert.Equal(65535, result.Port);
    }

    [Fact]
    public void Ipv6_host_port_is_parsed_from_bracketed_syntax()
    {
        var result = _normalizer.Normalize("http://[2001:db8::1]:8080/x").OrThrow();
        Assert.Equal("http://[2001:db8::1]:8080/x", result.IocValue);
        Assert.Equal(8080, result.Port);
    }

    [Fact]
    public void Default_ipv6_host_port_is_removed()
    {
        var result = _normalizer.Normalize("https://[2001:db8::1]:443/x").OrThrow();
        Assert.Equal("https://[2001:db8::1]/x", result.IocValue);
        Assert.Equal(443, result.Port);
    }

    [Theory]
    [InlineData("http://example.com:0/")]
    [InlineData("http://example.com:65536/")]
    [InlineData("http://example.com:-1/")]
    [InlineData("http://example.com:abc/")]
    [InlineData("http://example.com:80x/")]
    [InlineData("http://example.com:/")]
    [InlineData("https://[2001:db8::1]:0/")]
    public void Invalid_ports_are_rejected(string input)
    {
        var result = _normalizer.Normalize(input);
        Assert.True(result.IsError);
        Assert.Equal(NormalizationErrorCode.InvalidPort, result.Error.Code);
    }

    [Fact]
    public void Url_without_explicit_port_has_null_port()
    {
        var result = _normalizer.Normalize("https://example.com/a").OrThrow();
        Assert.Null(result.Port);
    }
}