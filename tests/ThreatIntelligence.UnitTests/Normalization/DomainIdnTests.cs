using ThreatIntelligence.Application.Normalization;
using ThreatIntelligence.Infrastructure.PublicSuffix;

namespace ThreatIntelligence.UnitTests.Normalization;

public class DomainIdnTests
{
    private readonly DomainNormalizer _normalizer = new(new PublicSuffixParentDomainResolver());

    [Theory]
    [InlineData("münchen.example")]
    [InlineData("bücher.example")]
    [InlineData("例え.テスト")]
    public void Unicode_idn_is_preserved_without_punycode_conversion(string input)
    {
        var result = _normalizer.Normalize(input);
        Assert.True(result.IsSuccess);
        Assert.Equal(input, result.Result.IocValue);
    }

    [Theory]
    [InlineData("xn--mnchen-3ya.example")]
    [InlineData("xn--bcher-kva.example")]
    public void Punycode_form_is_preserved_without_decoding(string input)
    {
        var result = _normalizer.Normalize(input);
        Assert.True(result.IsSuccess);
        Assert.Equal(input, result.Result.IocValue);
    }

    [Fact]
    public void Unicode_and_punycode_forms_remain_distinct_normalized_values()
    {
        var unicode = _normalizer.Normalize("münchen.example").OrThrow();
        var punycode = _normalizer.Normalize("xn--mnchen-3ya.example").OrThrow();

        Assert.NotEqual(unicode.IocValue, punycode.IocValue);
        Assert.Equal("münchen.example", unicode.IocValue);
        Assert.Equal("xn--mnchen-3ya.example", punycode.IocValue);
    }

    [Fact]
    public void Uppercase_unicode_is_lowercased_invariantly()
    {
        var result = _normalizer.Normalize("MÜNCHEN.Example").OrThrow();
        Assert.Equal("münchen.example", result.IocValue);
    }
}