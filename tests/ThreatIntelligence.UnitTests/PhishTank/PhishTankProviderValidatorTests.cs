using ThreatIntelligence.Application.ProviderRecords;
using ThreatIntelligence.Domain.Enums;
using ThreatIntelligence.Infrastructure.PhishTank;
using ThreatIntelligence.UnitTests.ProviderRecords;

namespace ThreatIntelligence.UnitTests.PhishTank;

public sealed class PhishTankProviderValidatorTests
{
    private readonly PhishTankProviderValidator _validator = new();
    private readonly ProviderRecordValidator _pipelineValidator;

    public PhishTankProviderValidatorTests()
    {
        _pipelineValidator = ProviderValidationFactory.CreateValidator(_validator);
    }

    private static ProviderRecordInput ValidInput() => new(
        ProviderId: PhishTankProviderValidator.PhishTankProviderId,
        ProviderRecordId: "12345",
        RawIoc: "http://evil.example.com/phish",
        DeclaredType: IocType.Url,
        FirstSeenAt: new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc),
        LastSeenAt: new DateTime(2026, 1, 1, 12, 30, 0, DateTimeKind.Utc),
        ProviderDetailsLink: "http://data.phishtank.com/data/12345/detail",
        Tags: null,
        Metadata: null,
        Payload: null);

    [Fact]
    public void Valid_input_passes_provider_validation()
    {
        var rejection = _validator.Validate(ValidInput());
        Assert.Null(rejection);
    }

    [Fact]
    public void Wrong_provider_id_is_rejected()
    {
        var input = ValidInput() with { ProviderId = 1 };
        var rejection = _validator.Validate(input);

        Assert.NotNull(rejection);
        Assert.Equal("provider", rejection.Field);
    }

    [Fact]
    public void Valid_input_passes_full_pipeline()
    {
        var result = _pipelineValidator.Validate(ValidInput(), recordIndex: 1);

        Assert.True(result.IsValid);
        Assert.NotNull(result.Normalized);
        Assert.Equal(PhishTankProviderValidator.PhishTankProviderId, result.Normalized!.ProviderId);
    }

    [Fact]
    public void Provider_id_mismatch_fails_full_pipeline()
    {
        var input = ValidInput() with { ProviderId = 1 };
        var result = _pipelineValidator.Validate(input, recordIndex: 1);

        Assert.False(result.IsValid);
        Assert.NotNull(result.Failure);
    }
}
