using ThreatIntelligence.Application.Normalization;
using ThreatIntelligence.Infrastructure.PublicSuffix;

namespace ThreatIntelligence.UnitTests.Normalization;

public class UrlHostTests
{
    private readonly UrlNormalizer _normalizer = new(new PublicSuffixParentDomainResolver());

    [Fact]
    public void Ipv4_host_is_canonicalized_as_ip_literal()
    {
        var result = _normalizer.Normalize("http://1.2.3.4/path").OrThrow();
        Assert.Equal("http://1.2.3.4/path", result.IocValue);
        Assert.Equal("1.2.3.4", result.UrlHost);
        Assert.Null(result.ParentDomain);
    }

    [Theory]
    [InlineData("http://01.2.3.4/")]
    [InlineData("http://1.2.3.04/")]
    [InlineData("http://256.0.0.1/")]
    [InlineData("http://1.2.3/")]
    public void Malformed_ipv4_host_is_rejected(string input)
    {
        var result = _normalizer.Normalize(input);
        Assert.True(result.IsError);
        Assert.Equal(NormalizationErrorCode.InvalidUrlHost, result.Error.Code);
    }

    [Fact]
    public void Ipv6_host_is_canonicalized_to_compressed_lowercase_in_brackets()
    {
        var result = _normalizer.Normalize("http://[2001:0DB8:0000:0000:0000:0000:0000:0001]").OrThrow();
        Assert.Equal("http://[2001:db8::1]", result.IocValue);
        Assert.Equal("[2001:db8::1]", result.UrlHost);
        Assert.Null(result.ParentDomain);
    }

    [Fact]
    public void Looped_ipv6_host_is_canonicalized()
    {
        var result = _normalizer.Normalize("http://[::1]").OrThrow();
        Assert.Equal("http://[::1]", result.IocValue);
        Assert.Equal("[::1]", result.UrlHost);
    }

    [Theory]
    [InlineData("http://[bogus]/")]
    [InlineData("http://[]/")]
    [InlineData("http://[2001:db8::1/")]
    [InlineData("http://[1.2.3.4]/")]
    [InlineData("http://[2001:db8::1]extra/")]
    public void Invalid_bracketed_host_is_rejected(string input)
    {
        var result = _normalizer.Normalize(input);
        Assert.True(result.IsError);
        Assert.Equal(NormalizationErrorCode.InvalidUrlHost, result.Error.Code);
    }

    [Fact]
    public void Unbracketed_ipv6_host_is_rejected()
    {
        var result = _normalizer.Normalize("http://2001:db8::1/");
        Assert.True(result.IsError);
        Assert.Equal(NormalizationErrorCode.InvalidUrlHost, result.Error.Code);
    }

    [Fact]
    public void Userinfo_is_rejected()
    {
        var result = _normalizer.Normalize("http://user:pass@example.com/");
        Assert.True(result.IsError);
        Assert.Equal(NormalizationErrorCode.InvalidUrlHost, result.Error.Code);
    }

    [Fact]
    public void Localhost_like_host_is_kept()
    {
        var result = _normalizer.Normalize("http://localhost/x").OrThrow();
        Assert.Equal("localhost", result.UrlHost);
        Assert.Equal("localhost", result.ParentDomain);
    }

    [Fact]
    public void Subdomain_host_is_lowercased_and_has_registrable_parent()
    {
        var result = _normalizer.Normalize("https://API.Example.CO.UK/x").OrThrow();
        Assert.Equal("api.example.co.uk", result.UrlHost);
        Assert.Equal("example.co.uk", result.ParentDomain);
    }

    [Fact]
    public void Unicode_host_is_preserved_without_punycode()
    {
        var result = _normalizer.Normalize("https://münchen.example").OrThrow();
        Assert.Equal("münchen.example", result.UrlHost);
        Assert.Equal("https://münchen.example", result.IocValue);
    }

    [Fact]
    public void Dns_host_over_253_characters_is_rejected()
    {
        var host = string.Join('.',
            new string('a', 63),
            new string('b', 63),
            new string('c', 63),
            new string('d', 62));

        var result = _normalizer.Normalize($"https://{host}");

        Assert.True(result.IsError);
        Assert.Equal(NormalizationErrorCode.InvalidUrlHost, result.Error!.Code);
    }
}
