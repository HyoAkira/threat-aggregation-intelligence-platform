using ThreatIntelligence.Application.Normalization;
using ThreatIntelligence.Infrastructure.PublicSuffix;

namespace ThreatIntelligence.UnitTests.Normalization;

public class DomainRejectionTests
{
    private readonly DomainNormalizer _normalizer = new(new PublicSuffixParentDomainResolver());

    [Theory]
    [InlineData(".example.com")]
    [InlineData("example..com")]
    [InlineData("example.com..")]
    [InlineData("example.com...")]
    public void Empty_labels_are_rejected(string input)
    {
        var result = _normalizer.Normalize(input);
        AssertFailure(result, NormalizationErrorCode.EmptyLabel, ErrorMessages.EmptyLabel);
    }

    [Theory]
    [InlineData("ex_ample.com")]
    [InlineData("ex!ample.com")]
    [InlineData("example.com#hash")]
    public void Invalid_characters_are_rejected(string input)
    {
        var result = _normalizer.Normalize(input);
        AssertFailure(result, NormalizationErrorCode.InvalidDomainCharacter, ErrorMessages.InvalidDomainCharacter);
    }

    [Theory]
    [InlineData("-example.com")]
    [InlineData("example-.com")]
    public void Hyphen_boundaries_are_rejected(string input)
    {
        var result = _normalizer.Normalize(input);
        AssertFailure(result, NormalizationErrorCode.HyphenBoundary, ErrorMessages.HyphenBoundary);
    }

    [Fact]
    public void Sixty_four_character_label_is_rejected()
    {
        var label = new string('a', 64);
        var result = _normalizer.Normalize($"{label}.com");
        AssertFailure(result, NormalizationErrorCode.LabelTooLong, ErrorMessages.LabelTooLong);
    }

    [Fact]
    public void Two_hundred_fifty_three_character_domain_is_accepted()
    {
        var domain = $"{new string('a', 63)}.{new string('b', 63)}.{new string('c', 63)}.{new string('d', 61)}";
        Assert.Equal(253, domain.Length);
        var result = _normalizer.Normalize(domain);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Two_hundred_fifty_four_character_domain_is_rejected()
    {
        var domain = $"{new string('a', 63)}.{new string('b', 63)}.{new string('c', 63)}.{new string('d', 62)}";
        Assert.Equal(254, domain.Length);
        var result = _normalizer.Normalize(domain);
        AssertFailure(result, NormalizationErrorCode.DomainTooLong, ErrorMessages.DomainTooLong);
    }

    [Theory]
    [InlineData("http://example.com")]
    [InlineData("https://example.com")]
    [InlineData("ftp://example.com")]
    [InlineData("example.com://foo")]
    public void Schemes_are_rejected(string input)
    {
        var result = _normalizer.Normalize(input);
        AssertFailure(result, NormalizationErrorCode.DomainScheme, ErrorMessages.DomainScheme);
    }

    [Theory]
    [InlineData("example.com/path")]
    [InlineData("example.com/a/b")]
    public void Paths_are_rejected(string input)
    {
        var result = _normalizer.Normalize(input);
        AssertFailure(result, NormalizationErrorCode.DomainPath, ErrorMessages.DomainPath);
    }

    [Theory]
    [InlineData("example.com:443")]
    [InlineData("example.com:80")]
    public void Ports_are_rejected(string input)
    {
        var result = _normalizer.Normalize(input);
        AssertFailure(result, NormalizationErrorCode.DomainPort, ErrorMessages.DomainPort);
    }

    [Theory]
    [InlineData("example .com")]
    [InlineData("example\t.com")]
    [InlineData("exa mple.com")]
    public void Internal_whitespace_is_rejected(string input)
    {
        var result = _normalizer.Normalize(input);
        AssertFailure(result, NormalizationErrorCode.DomainWhitespace, ErrorMessages.DomainWhitespace);
    }

    [Theory]
    [InlineData("com")]
    [InlineData("localhost")]
    [InlineData("co")]
    public void Tld_only_values_are_rejected(string input)
    {
        var result = _normalizer.Normalize(input);
        AssertFailure(result, NormalizationErrorCode.TldOnly, ErrorMessages.TldOnly);
    }

    [Theory]
    [InlineData("co.uk")]
    [InlineData("foo.ck")]
    [InlineData("com.au")]
    public void Public_suffix_only_values_are_rejected(string input)
    {
        var result = _normalizer.Normalize(input);
        AssertFailure(result, NormalizationErrorCode.NoRegistrableDomain, ErrorMessages.NoRegistrableDomain);
    }

    [Fact]
    public void Blank_input_is_rejected()
    {
        AssertFailure(_normalizer.Normalize(""), NormalizationErrorCode.InvalidInput, ErrorMessages.InvalidInput);
        AssertFailure(_normalizer.Normalize("   "), NormalizationErrorCode.InvalidInput, ErrorMessages.InvalidInput);
    }

    private static void AssertFailure(
        NormalizationOutput output,
        NormalizationErrorCode code,
        string message)
    {
        Assert.True(output.IsError);
        Assert.Equal(code, output.Error.Code);
        Assert.Equal(message, output.Error.Message);
    }
}