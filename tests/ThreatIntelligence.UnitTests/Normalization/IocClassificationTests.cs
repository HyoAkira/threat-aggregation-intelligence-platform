using ThreatIntelligence.Application.Normalization;
using ThreatIntelligence.Domain.Enums;
using ThreatIntelligence.Infrastructure.PublicSuffix;

namespace ThreatIntelligence.UnitTests.Normalization;

public class IocClassificationTests
{
    private static IocClassifier CreateClassifier()
    {
        var resolver = new PublicSuffixParentDomainResolver();
        return new IocClassifier(
            new Ipv4Normalizer(),
            new Ipv6Normalizer(),
            new UrlNormalizer(resolver),
            new DomainNormalizer(resolver));
    }

    [Theory]
    [InlineData("1.2.3.4")]
    [InlineData(" 1.2.3.4 ")]
    [InlineData("1.2.3.4:443")]
    [InlineData("1.2.3.4:1")]
    [InlineData("1.2.3.4:65535")]
    public void Bare_and_port_bearing_ipv4_classify_as_ipv4(string input)
    {
        var result = CreateClassifier().Classify(input);
        Assert.True(result.IsSuccess);
        Assert.Equal(IocType.Ipv4, result.Result.Type);
    }

    [Theory]
    [InlineData("2001:db8::1")]
    [InlineData("::1")]
    [InlineData("[2001:db8::1]:443")]
    [InlineData("[::1]:65535")]
    public void Bare_and_bracketed_port_ipv6_classify_as_ipv6(string input)
    {
        var result = CreateClassifier().Classify(input);
        Assert.True(result.IsSuccess);
        Assert.Equal(IocType.Ipv6, result.Result.Type);
    }

    [Fact]
    public void Expanded_ipv6_classifies_as_ipv6_with_compressed_value()
    {
        var result = CreateClassifier().Classify("2001:0DB8:0000:0000:0000:0000:0000:0001").OrThrow();
        Assert.Equal(IocType.Ipv6, result.Type);
        Assert.Equal("2001:db8::1", result.IocValue);
    }

    [Theory]
    [InlineData("http://example.com/a")]
    [InlineData("https://example.com:443/a")]
    [InlineData("HTTP://Example.COM/a")]
    public void Url_marker_inputs_classify_as_url(string input)
    {
        var result = CreateClassifier().Classify(input);
        Assert.True(result.IsSuccess);
        Assert.Equal(IocType.Url, result.Result.Type);
    }

    [Theory]
    [InlineData("ftp://example.com")]
    [InlineData("mailto://x")]
    [InlineData("http://")]
    [InlineData("http://:8080")]
    public void Malformed_or_unsupported_url_markers_return_url_errors_and_no_domain_fallback(string input)
    {
        var result = CreateClassifier().Classify(input);
        Assert.True(result.IsError);
        Assert.NotEqual(NormalizationErrorCode.InvalidInput, result.Error.Code);
    }

    [Fact]
    public void Url_marker_failure_keeps_the_url_specific_code()
    {
        var result = CreateClassifier().Classify("ftp://example.com");
        Assert.True(result.IsError);
        Assert.Equal(NormalizationErrorCode.InvalidUrlScheme, result.Error.Code);
        Assert.Equal(ErrorMessages.InvalidUrlScheme, result.Error.Message);
    }

    [Fact]
    public void Unsupported_scheme_marker_does_not_fall_through_to_domain()
    {
        var result = CreateClassifier().Classify("example.com://x");
        Assert.True(result.IsError);
        Assert.Equal(NormalizationErrorCode.InvalidUrlScheme, result.Error.Code);
    }

    [Theory]
    [InlineData("example.com")]
    [InlineData("Api.Example.CO.UK.")]
    [InlineData("www.example.net")]
    public void Valid_domains_classify_as_domain(string input)
    {
        var result = CreateClassifier().Classify(input);
        Assert.True(result.IsSuccess);
        Assert.Equal(IocType.Domain, result.Result.Type);
    }

    [Fact]
    public void Domain_input_is_normalized_invariantly()
    {
        var result = CreateClassifier().Classify("Api.Example.CO.UK.").OrThrow();
        Assert.Equal("api.example.co.uk", result.IocValue);
        Assert.Equal("example.co.uk", result.ParentDomain);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("exa_mple.com")]
    [InlineData(":(")]
    [InlineData("example .com")]
    public void Invalid_residual_input_returns_stable_invalid_input(string input)
    {
        var result = CreateClassifier().Classify(input);
        Assert.True(result.IsError);
        Assert.Equal(NormalizationErrorCode.InvalidInput, result.Error.Code);
        Assert.Equal(ErrorMessages.InvalidInput, result.Error.Message);
    }

    [Fact]
    public void Ipv4_classification_matches_direct_ipv4_normalizer_output()
    {
        var classifier = CreateClassifier();
        var direct = new Ipv4Normalizer().Normalize("1.2.3.4:443").OrThrow();
        var classified = classifier.Classify("1.2.3.4:443").OrThrow();

        Assert.Equal(direct, classified);
    }

    [Fact]
    public void Url_failure_is_an_application_level_error_without_http_status()
    {
        var result = CreateClassifier().Classify("http://");
        Assert.True(result.IsError);
        Assert.NotNull(result.Error);
        Assert.Equal(NormalizationErrorCode.MissingUrlHost, result.Error.Code);
        Assert.Equal(ErrorMessages.MissingUrlHost, result.Error.Message);
    }
}