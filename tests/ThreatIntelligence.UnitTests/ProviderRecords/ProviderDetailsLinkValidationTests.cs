using ThreatIntelligence.Application.ProviderRecords;

namespace ThreatIntelligence.UnitTests.ProviderRecords;

public class ProviderDetailsLinkValidationTests
{
    [Fact]
    public void Null_details_link_passes_and_yields_null_link()
    {
        var input = ProviderValidationFactory.ValidInput() with { ProviderDetailsLink = null };

        var result = ProviderValidationFactory.CreateValidatorWithDefault().Validate(input, recordIndex: 1);

        Assert.True(result.IsValid);
        Assert.Null(result.Normalized.ProviderDetailsLink);
    }

    [Theory]
    [InlineData("https://threatfox.example/ioc123")]
    [InlineData("http://vigilance.example/domains/api.example.co.uk")]
    public void Http_and_https_links_pass(string link)
    {
        var input = ProviderValidationFactory.ValidInput() with { ProviderDetailsLink = link };

        var result = ProviderValidationFactory.CreateValidatorWithDefault().Validate(input, recordIndex: 1);

        Assert.True(result.IsValid);
        Assert.Equal(link, result.Normalized.ProviderDetailsLink);
    }

    [Fact]
    public void Link_of_exactly_2048_characters_passes()
    {
        var link = "https://example.com/" + new string('a', 2028);
        var input = ProviderValidationFactory.ValidInput() with { ProviderDetailsLink = link };

        var result = ProviderValidationFactory.CreateValidatorWithDefault().Validate(input, recordIndex: 1);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Link_over_2048_characters_fails_as_too_long()
    {
        var link = "https://example.com/" + new string('a', 2029);
        var input = ProviderValidationFactory.ValidInput() with { ProviderDetailsLink = link };

        var result = ProviderValidationFactory.CreateValidatorWithDefault().Validate(input, recordIndex: 1);

        Assert.False(result.IsValid);
        Assert.Equal(ProviderRecordFailureCode.DetailsLinkTooLong, result.Failure.Code);
        Assert.Equal("providerDetailsLink", result.Failure.Field);
    }

    [Theory]
    [InlineData("ftp://example.com/file")]
    [InlineData("javascript:alert(1)")]
    [InlineData("not a url")]
    [InlineData("example.com/ioc")]
    public void Non_http_or_malformed_links_fail_as_invalid(string link)
    {
        var input = ProviderValidationFactory.ValidInput() with { ProviderDetailsLink = link };

        var result = ProviderValidationFactory.CreateValidatorWithDefault().Validate(input, recordIndex: 1);

        Assert.False(result.IsValid);
        Assert.Equal(ProviderRecordFailureCode.InvalidDetailsLink, result.Failure.Code);
        Assert.Equal("providerDetailsLink", result.Failure.Field);
    }

    [Fact]
    public void Link_containing_user_information_fails_as_invalid()
    {
        var input = ProviderValidationFactory.ValidInput() with
        {
            ProviderDetailsLink = "https://user:password@example.com/ioc"
        };

        var result = ProviderValidationFactory.CreateValidatorWithDefault().Validate(input, recordIndex: 1);

        Assert.False(result.IsValid);
        Assert.Equal(ProviderRecordFailureCode.InvalidDetailsLink, result.Failure.Code);
    }

    [Fact]
    public void Oversized_malformed_link_fails_as_too_long_before_structure_is_checked()
    {
        var link = "https://example.com/" + new string('z', 2100) + " not a close";
        var input = ProviderValidationFactory.ValidInput() with { ProviderDetailsLink = link };

        var result = ProviderValidationFactory.CreateValidatorWithDefault().Validate(input, recordIndex: 1);

        Assert.Equal(ProviderRecordFailureCode.DetailsLinkTooLong, result.Failure.Code);
    }
}