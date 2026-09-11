using ThreatIntelligence.Application.ProviderRecords;
using ThreatIntelligence.Domain.Enums;
using ThreatIntelligence.Infrastructure.ThreatFox;
using ThreatIntelligence.UnitTests.ProviderRecords;

namespace ThreatIntelligence.UnitTests.ThreatFox;

public sealed class ThreatFoxProviderValidatorTests
{
    private readonly ThreatFoxProviderValidator _validator = new();

    private static ProviderRecordInput ValidInput() => new(
        ProviderId: ThreatFoxProviderValidator.ThreatFoxProviderId,
        ProviderRecordId: "ioc-1020675",
        RawIoc: "http://116.202.5.101/1679",
        DeclaredType: IocType.Url,
        FirstSeenAt: new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc),
        LastSeenAt: null,
        ProviderDetailsLink: "https://threatfox.abuse.ch/ioc/1020675",
        Tags: ["botnet"],
        Metadata: new Dictionary<string, object?> { ["confidence_level"] = 50, ["malware"] = "win.vidar" },
        Payload: null);

    [Fact]
    public void Valid_threatfox_record_is_accepted()
    {
        Assert.Null(_validator.Validate(ValidInput()));
    }

    [Fact]
    public void Non_threatfox_record_is_rejected_fail_closed()
    {
        var rejection = _validator.Validate(ValidInput() with { ProviderId = 2 });

        Assert.NotNull(rejection);
        Assert.Equal("provider", rejection.Field);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Confidence_level_outside_zero_to_one_hundred_is_rejected(int confidence)
    {
        var input = ValidInput() with
        {
            Metadata = new Dictionary<string, object?> { ["confidence_level"] = confidence }
        };

        var rejection = _validator.Validate(input);

        Assert.NotNull(rejection);
        Assert.Equal(ThreatFoxRecordMapper.ConfidenceLevelKey, rejection.Field);
    }

    [Fact]
    public void Confidence_zero_is_accepted()
    {
        var input = ValidInput() with
        {
            Metadata = new Dictionary<string, object?> { ["confidence_level"] = 0 }
        };

        Assert.Null(_validator.Validate(input));
    }

    [Fact]
    public void Absent_confidence_is_accepted()
    {
        Assert.Null(_validator.Validate(ValidInput() with { Metadata = null }));
    }

    [Fact]
    public void Record_passes_the_e03_pipeline_with_the_threatfox_validator_registered()
    {
        var pipeline = ProviderValidationFactory.CreateValidator(_validator);

        var result = pipeline.Validate(ValidInput(), recordIndex: 1);

        Assert.True(result.IsValid);
        Assert.Equal("win.vidar", result.Normalized!.Metadata!["malware"]);
    }

    [Fact]
    public void Record_with_bad_confidence_fails_with_the_safe_e03_failure_contract()
    {
        var pipeline = ProviderValidationFactory.CreateValidator(_validator);
        var input = ValidInput() with
        {
            Metadata = new Dictionary<string, object?> { ["confidence_level"] = 101 }
        };

        var result = pipeline.Validate(input, recordIndex: 3);

        Assert.False(result.IsValid);
        Assert.NotNull(result.Failure);
        Assert.Equal(3, result.Failure.RecordIndex);
        Assert.Equal(ThreatFoxProviderValidator.ThreatFoxProviderId, result.Failure.ProviderId);
        Assert.Equal("ioc-1020675", result.Failure.ProviderRecordId);
        Assert.Equal(ProviderRecordFailureCode.ProviderRejected, result.Failure.Code);
        Assert.Equal("payload", result.Failure.Field);
        Assert.DoesNotContain("http://116.202.5.101", result.Failure.Message);
    }

    [Fact]
    public void A_threatfox_record_flows_through_to_the_shared_validator_as_valid()
    {
        var pipeline = ProviderValidationFactory.CreateValidator(_validator);
        var input = new ProviderRecordInput(
            ProviderId: 1,
            ProviderRecordId: "10",
            RawIoc: "185.132.53.205:1312",
            DeclaredType: null,
            FirstSeenAt: new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc),
            LastSeenAt: null,
            ProviderDetailsLink: "https://threatfox.abuse.ch/ioc/10",
            Tags: null,
            Metadata: new Dictionary<string, object?> { ["confidence_level"] = 0 },
            Payload: null);

        var result = pipeline.Validate(input, recordIndex: 1);

        Assert.True(result.IsValid);
        Assert.Equal(IocType.Ipv4, result.Normalized!.IocIdentity.Type);
        Assert.Equal(1312, result.Normalized.IocIdentity.Port);
    }
}