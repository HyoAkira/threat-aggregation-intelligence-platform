using ThreatIntelligence.Application.Normalization;
using ThreatIntelligence.Domain.Enums;
using ThreatIntelligence.Infrastructure.PublicSuffix;

namespace ThreatIntelligence.UnitTests.Normalization;

public class UrlNormalizationTests
{
    private readonly UrlNormalizer _normalizer = new(new PublicSuffixParentDomainResolver());

    [Theory]
    [InlineData("https://example.com", "https://example.com")]
    [InlineData("http://example.com", "http://example.com")]
    [InlineData("  https://example.com  ", "https://example.com")]
    public void Absolute_http_or_https_urls_are_accepted(string input, string expected)
    {
        var result = _normalizer.Normalize(input);
        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.Result.IocValue);
    }

    [Theory]
    [InlineData("HTTPS://Example.COM")]
    [InlineData("Https://Example.COM")]
    [InlineData("HTTP://Example.COM")]
    public void Scheme_and_host_are_lowercased(string input)
    {
        var result = _normalizer.Normalize(input);
        Assert.True(result.IsSuccess);
        Assert.Equal(input.ToLowerInvariant(), result.Result.IocValue);
    }

    [Fact]
    public void Canonical_url_result_shape()
    {
        var result = _normalizer.Normalize("HTTPS://Api.Example.CO.UK:443/a").OrThrow();

        Assert.Equal(IocType.Url, result.Type);
        Assert.Equal("https://api.example.co.uk/a", result.IocValue);
        Assert.Equal(443, result.Port);
        Assert.Equal("api.example.co.uk", result.UrlHost);
        Assert.Equal("example.co.uk", result.ParentDomain);
    }

    [Fact]
    public void Path_and_query_case_remain_unchanged()
    {
        var result = _normalizer.Normalize("https://example.com/PaTh/WithUpperCase?Query=MiXeD").OrThrow();
        Assert.Equal("https://example.com/PaTh/WithUpperCase?Query=MiXeD", result.IocValue);
    }

    [Theory]
    [InlineData("ftp://example.com")]
    [InlineData("mailto://example.com")]
    [InlineData("javascript://x")]
    [InlineData("example.com")]
    [InlineData("::1")]
    [InlineData("://missing-scheme")]
    public void Unsupported_or_missing_schemes_are_rejected(string input)
    {
        var result = _normalizer.Normalize(input);
        Assert.True(result.IsError);
        Assert.Equal(NormalizationErrorCode.InvalidUrlScheme, result.Error.Code);
    }

    [Theory]
    [InlineData("http:///path")]
    [InlineData("http://?q=1")]
    [InlineData("http://#frag")]
    [InlineData("http://:80/")]
    public void Missing_host_is_rejected(string input)
    {
        var result = _normalizer.Normalize(input);
        Assert.True(result.IsError);
        Assert.Equal(NormalizationErrorCode.MissingUrlHost, result.Error.Code);
    }

    [Fact]
    public void Empty_input_is_invalid()
    {
        var result = _normalizer.Normalize("   ");
        Assert.True(result.IsError);
        Assert.Equal(NormalizationErrorCode.InvalidInput, result.Error.Code);
    }
}