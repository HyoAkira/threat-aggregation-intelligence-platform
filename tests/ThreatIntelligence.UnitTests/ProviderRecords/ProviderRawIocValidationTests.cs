using ThreatIntelligence.Application.ProviderRecords;
using ThreatIntelligence.Domain.Enums;

namespace ThreatIntelligence.UnitTests.ProviderRecords;

public class ProviderRawIocValidationTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\n\t ")]
    public void Blank_raw_ioc_fails_with_blank_raw_ioc(string rawIoc)
    {
        var input = ProviderValidationFactory.ValidInput() with { RawIoc = rawIoc };

        var result = ProviderValidationFactory.CreateValidatorWithDefault().Validate(input, recordIndex: 1);

        Assert.False(result.IsValid);
        Assert.Equal(ProviderRecordFailureCode.BlankRawIoc, result.Failure.Code);
        Assert.Equal("rawIoc", result.Failure.Field);
    }

    [Fact]
    public void Blank_raw_ioc_fails_before_normalization_even_with_a_declared_type()
    {
        var input = ProviderValidationFactory.ValidInput() with
        {
            RawIoc = "  ",
            DeclaredType = IocType.Url
        };

        var result = ProviderValidationFactory.CreateValidatorWithDefault().Validate(input, recordIndex: 1);

        Assert.Equal(ProviderRecordFailureCode.BlankRawIoc, result.Failure.Code);
    }
}