using ThreatIntelligence.Application.Normalization;

namespace ThreatIntelligence.UnitTests.Normalization;

public class UrlComponentTests
{
    private readonly UrlNormalizer _normalizer = new(new ThreatIntelligence.Infrastructure.PublicSuffix.PublicSuffixParentDomainResolver());

    [Theory]
    [InlineData("https://example.com/a#frag", "https://example.com/a")]
    [InlineData("https://example.com/#frag", "https://example.com")]
    [InlineData("https://example.com/a?b=1#frag", "https://example.com/a?b=1")]
    public void Fragments_are_stripped(string input, string expected)
    {
        var result = _normalizer.Normalize(input).OrThrow();
        Assert.Equal(expected, result.IocValue);
    }

    [Fact]
    public void Fragment_stripping_preserves_every_other_component()
    {
        var input = "https://ExAmPLe.COM/PaTh/To?Query=Value&x=1#fragment";
        var result = _normalizer.Normalize(input).OrThrow();

        Assert.Equal("https://example.com/PaTh/To?Query=Value&x=1", result.IocValue);
    }

    [Theory]
    [InlineData("https://example.com/", "https://example.com")]
    [InlineData("https://example.com", "https://example.com")]
    [InlineData("https://example.com/?q=1", "https://example.com?q=1")]
    public void Empty_root_slash_is_dropped(string input, string expected)
    {
        var result = _normalizer.Normalize(input).OrThrow();
        Assert.Equal(expected, result.IocValue);
    }

    [Theory]
    [InlineData("https://example.com/a", "https://example.com/a")]
    [InlineData("https://example.com/a/", "https://example.com/a/")]
    [InlineData("https://example.com/a/b/c", "https://example.com/a/b/c")]
    [InlineData("https://example.com//", "https://example.com//")]
    public void Non_root_paths_are_preserved_unchanged(string input, string expected)
    {
        var result = _normalizer.Normalize(input).OrThrow();
        Assert.Equal(expected, result.IocValue);
    }

    [Theory]
    [InlineData("https://example.com/a%2Fb%20c", "https://example.com/a%2Fb%20c")]
    [InlineData("https://example.com/x?q=%2F%26%3D", "https://example.com/x?q=%2F%26%3D")]
    [InlineData("https://example.com/x?a=1&b=2&c=3", "https://example.com/x?a=1&b=2&c=3")]
    [InlineData("https://example.com/x?a=%41%42", "https://example.com/x?a=%41%42")]
    public void Path_and_query_delimiters_and_percent_encoding_are_not_modified(string input, string expected)
    {
        var result = _normalizer.Normalize(input).OrThrow();
        Assert.Equal(expected, result.IocValue);
    }

    [Theory]
    [InlineData("https://example.com/a b")]
    [InlineData("https://example.com/a\tb")]
    [InlineData("https://example.com/a?x=hello world")]
    [InlineData("https://example.com/a?x=hello\r\nworld")]
    public void Raw_whitespace_and_control_characters_in_path_or_query_are_rejected(string input)
    {
        var result = _normalizer.Normalize(input);

        Assert.True(result.IsError);
        Assert.Equal(NormalizationErrorCode.InvalidUrlCharacter, result.Error!.Code);
    }
}
