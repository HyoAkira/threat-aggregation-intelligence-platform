using ThreatIntelligence.Application.Normalization;

namespace ThreatIntelligence.UnitTests.Normalization;

public class UrlLengthTests
{
    private readonly UrlNormalizer _normalizer = new(new ThreatIntelligence.Infrastructure.PublicSuffix.PublicSuffixParentDomainResolver());

    [Fact]
    public void Exactly_2048_characters_is_accepted()
    {
        var result = _normalizer.Normalize(BuildUrl(2048));
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Over_2048_characters_is_rejected()
    {
        var result = _normalizer.Normalize(BuildUrl(2049));
        Assert.True(result.IsError);
        Assert.Equal(NormalizationErrorCode.UrlTooLong, result.Error.Code);
    }

    [Fact]
    public void Limit_applies_to_normalized_length_not_raw_input()
    {
        var normalized = BuildUrl(2048);
        var withFragment = normalized + "#fragment";
        Assert.True(withFragment.Length > 2048);

        var result = _normalizer.Normalize(withFragment);
        Assert.True(result.IsSuccess);
    }

    private static string BuildUrl(int totalLength)
    {
        const string baseUrl = "https://example.com/";
        var pathPadding = totalLength - baseUrl.Length;
        return baseUrl + new string('a', pathPadding);
    }
}