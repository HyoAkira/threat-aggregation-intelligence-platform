using ThreatIntelligence.Application.Normalization;
using ThreatIntelligence.Domain.Enums;
using ThreatIntelligence.Infrastructure.PublicSuffix;

namespace ThreatIntelligence.UnitTests.Normalization;

public class DomainNormalizationTests
{
    private readonly DomainNormalizer _normalizer = new(new PublicSuffixParentDomainResolver());

    [Theory]
    [InlineData("example.com", "example.com")]
    [InlineData("EXAMPLE.COM", "example.com")]
    [InlineData("example.com.", "example.com")]
    [InlineData("Api.Example.CO.UK.", "api.example.co.uk")]
    [InlineData("  example.com  ", "example.com")]
    public void Valid_domain_is_trimmed_lowercased_and_has_one_trailing_dot_stripped(string input, string expected)
    {
        var result = _normalizer.Normalize(input);
        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.Result.IocValue);
    }

    [Fact]
    public void Domain_result_has_correct_type_and_relation_fields()
    {
        var result = _normalizer.Normalize("Api.Example.CO.UK.").OrThrow();
        Assert.Equal(IocType.Domain, result.Type);
        Assert.Equal("api.example.co.uk", result.IocValue);
        Assert.Null(result.Port);
        Assert.Null(result.UrlHost);
        Assert.Equal("example.co.uk", result.ParentDomain);
    }

    [Theory]
    [InlineData("www.example.com")]
    [InlineData("a-b.example.com")]
    [InlineData("a--b.example.com")]
    [InlineData("digits123.example.com")]
    public void Valid_dns_labels_are_accepted(string input)
    {
        var result = _normalizer.Normalize(input);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Sixty_three_character_label_is_accepted()
    {
        var label = new string('a', 63);
        var result = _normalizer.Normalize($"{label}.com");
        Assert.True(result.IsSuccess);
    }
}