using ThreatIntelligence.Application.ProviderRecords;

namespace ThreatIntelligence.UnitTests.ProviderRecords;

public class ProviderIdentityValidationTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Non_positive_provider_id_fails_with_invalid_provider_id(int providerId)
    {
        var input = ProviderValidationFactory.ValidInput() with { ProviderId = providerId };

        var result = ProviderValidationFactory.CreateValidatorWithDefault().Validate(input, recordIndex: 1);

        Assert.False(result.IsValid);
        Assert.Equal(ProviderRecordFailureCode.InvalidProviderId, result.Failure.Code);
        Assert.Equal("providerId", result.Failure.Field);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void Blank_provider_record_id_fails_with_blank_provider_record_id(string providerRecordId)
    {
        var input = ProviderValidationFactory.ValidInput() with { ProviderRecordId = providerRecordId };

        var result = ProviderValidationFactory.CreateValidatorWithDefault().Validate(input, recordIndex: 1);

        Assert.False(result.IsValid);
        Assert.Equal(ProviderRecordFailureCode.BlankProviderRecordId, result.Failure.Code);
        Assert.Equal("providerRecordId", result.Failure.Field);
    }

    [Fact]
    public void Null_provider_record_id_returns_a_structured_blank_identity_failure()
    {
        var input = ProviderValidationFactory.ValidInput() with { ProviderRecordId = null };

        var result = ProviderValidationFactory.CreateValidatorWithDefault().Validate(input, recordIndex: 1);

        Assert.False(result.IsValid);
        Assert.Equal(ProviderRecordFailureCode.BlankProviderRecordId, result.Failure!.Code);
        Assert.Equal(string.Empty, result.Failure.ProviderRecordId);
    }

    [Fact]
    public void Provider_record_id_over_100_characters_fails_as_too_long()
    {
        var input = ProviderValidationFactory.ValidInput() with { ProviderRecordId = new string('p', 101) };

        var result = ProviderValidationFactory.CreateValidatorWithDefault().Validate(input, recordIndex: 1);

        Assert.False(result.IsValid);
        Assert.Equal(ProviderRecordFailureCode.ProviderRecordIdTooLong, result.Failure.Code);
        Assert.Equal("providerRecordId", result.Failure.Field);
    }

    [Fact]
    public void Provider_record_id_of_exactly_100_characters_passes()
    {
        var input = ProviderValidationFactory.ValidInput() with { ProviderRecordId = new string('p', 100) };

        var result = ProviderValidationFactory.CreateValidatorWithDefault().Validate(input, recordIndex: 1);

        Assert.True(result.IsValid);
        Assert.Equal(new string('p', 100), result.Normalized.ProviderRecordId);
    }

    [Fact]
    public void Invalid_provider_id_fails_before_blank_raw_ioc_is_checked()
    {
        var input = ProviderValidationFactory.ValidInput() with
        {
            ProviderId = -1,
            RawIoc = "   "
        };

        var result = ProviderValidationFactory.CreateValidatorWithDefault().Validate(input, recordIndex: 1);

        Assert.Equal(ProviderRecordFailureCode.InvalidProviderId, result.Failure.Code);
    }

    [Fact]
    public void Blank_provider_record_id_fails_before_blank_raw_ioc_is_checked()
    {
        var input = ProviderValidationFactory.ValidInput() with
        {
            ProviderRecordId = "  ",
            RawIoc = ""
        };

        var result = ProviderValidationFactory.CreateValidatorWithDefault().Validate(input, recordIndex: 1);

        Assert.Equal(ProviderRecordFailureCode.BlankProviderRecordId, result.Failure.Code);
    }

    [Fact]
    public void Oversized_provider_record_id_fails_before_normalization()
    {
        var input = ProviderValidationFactory.ValidInput() with
        {
            ProviderRecordId = new string('p', 200),
            RawIoc = "this is not a valid ioc"
        };

        var result = ProviderValidationFactory.CreateValidatorWithDefault().Validate(input, recordIndex: 1);

        Assert.Equal(ProviderRecordFailureCode.ProviderRecordIdTooLong, result.Failure.Code);
    }
}
