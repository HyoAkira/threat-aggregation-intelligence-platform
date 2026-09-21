using ThreatIntelligence.Application.ProviderRecords;

namespace ThreatIntelligence.UnitTests.ProviderRecords;

public class ProviderTagValidationTests
{
    [Fact]
    public void Tags_are_trimmed_lowercased_deduplicated_and_blanks_removed()
    {
        var input = ProviderValidationFactory.ValidInput() with
        {
            Tags = new[] { " BotNet ", "botnet ", "EVIL", "", "   ", "Ai", "ai", "EvIl " }
        };

        var result = ProviderValidationFactory.CreateValidatorWithDefault().Validate(input, recordIndex: 1);

        Assert.True(result.IsValid);
        Assert.Equal(new[] { "botnet", "evil", "ai" }, result.Normalized.Tags);
    }

    [Fact]
    public void Null_tags_yield_an_empty_list()
    {
        var input = ProviderValidationFactory.ValidInput() with { Tags = null };

        var result = ProviderValidationFactory.CreateValidatorWithDefault().Validate(input, recordIndex: 1);

        Assert.True(result.IsValid);
        Assert.NotNull(result.Normalized.Tags);
        Assert.Empty(result.Normalized.Tags);
    }

    [Fact]
    public void Empty_tags_yield_an_empty_list()
    {
        var input = ProviderValidationFactory.ValidInput() with { Tags = Array.Empty<string>() };

        var result = ProviderValidationFactory.CreateValidatorWithDefault().Validate(input, recordIndex: 1);

        Assert.True(result.IsValid);
        Assert.NotNull(result.Normalized.Tags);
        Assert.Empty(result.Normalized.Tags);
    }

    [Fact]
    public void Null_tag_items_are_treated_as_blank_and_removed()
    {
        IReadOnlyCollection<string?> tags = [" BotNet ", null, " ", "botnet"];
        var input = ProviderValidationFactory.ValidInput() with { Tags = tags };

        var result = ProviderValidationFactory.CreateValidatorWithDefault().Validate(input, recordIndex: 1);

        Assert.True(result.IsValid);
        Assert.Equal(new[] { "botnet" }, result.Normalized!.Tags);
    }

    [Fact]
    public void Tag_longer_than_100_characters_fails_as_invalid_tag()
    {
        var input = ProviderValidationFactory.ValidInput() with
        {
            Tags = new[] { new string('t', 101) }
        };

        var result = ProviderValidationFactory.CreateValidatorWithDefault().Validate(input, recordIndex: 1);

        Assert.False(result.IsValid);
        Assert.Equal(ProviderRecordFailureCode.InvalidTag, result.Failure.Code);
        Assert.Equal("tags", result.Failure.Field);
    }

    [Fact]
    public void Tag_of_exactly_100_characters_passes()
    {
        var input = ProviderValidationFactory.ValidInput() with
        {
            Tags = new[] { new string('t', 100) }
        };

        var result = ProviderValidationFactory.CreateValidatorWithDefault().Validate(input, recordIndex: 1);

        Assert.True(result.IsValid);
        Assert.Equal(new[] { new string('t', 100) }, result.Normalized.Tags);
    }

    [Fact]
    public void Leading_whitespace_is_applied_before_the_length_limit_is_checked()
    {
        var input = ProviderValidationFactory.ValidInput() with
        {
            Tags = new[] { " " + new string('t', 100) }
        };

        var result = ProviderValidationFactory.CreateValidatorWithDefault().Validate(input, recordIndex: 1);

        Assert.True(result.IsValid);
        Assert.Equal(new[] { new string('t', 100) }, result.Normalized.Tags);
    }

    [Fact]
    public void A_tag_over_100_characters_after_trimming_still_fails()
    {
        var input = ProviderValidationFactory.ValidInput() with
        {
            Tags = new[] { " " + new string('t', 101) }
        };

        var result = ProviderValidationFactory.CreateValidatorWithDefault().Validate(input, recordIndex: 1);

        Assert.False(result.IsValid);
        Assert.Equal(ProviderRecordFailureCode.InvalidTag, result.Failure.Code);
    }
}
