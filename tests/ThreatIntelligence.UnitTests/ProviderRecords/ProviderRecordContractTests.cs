using ThreatIntelligence.Application.Normalization;
using ThreatIntelligence.Application.ProviderRecords;
using ThreatIntelligence.Domain.Enums;

namespace ThreatIntelligence.UnitTests.ProviderRecords;

public class ProviderRecordContractTests
{
    private static readonly IReadOnlyDictionary<string, object?> Payload = new Dictionary<string, object?>
    {
        ["threat_type"] = "botnet_cc",
        ["malware"] = "trickbot"
    };

    [Fact]
    public void Input_contract_exposes_every_common_field_and_an_opaque_payload()
    {
        var input = new ProviderRecordInput(
            ProviderId: 1,
            ProviderRecordId: "ioc-123",
            RawIoc: "  Api.Example.CO.UK.  ",
            DeclaredType: IocType.Domain,
            FirstSeenAt: new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc),
            LastSeenAt: new DateTime(2026, 9, 7, 10, 30, 0, DateTimeKind.Utc),
            ProviderDetailsLink: "https://threatfox.example/ioc123",
            Tags: new[] { " BotNet ", "botnet " },
            Metadata: new Dictionary<string, object?> { ["reporter"] = "system" },
            Payload);

        Assert.Equal(1, input.ProviderId);
        Assert.Equal("ioc-123", input.ProviderRecordId);
        Assert.Equal("  Api.Example.CO.UK.  ", input.RawIoc);
        Assert.Equal(IocType.Domain, input.DeclaredType);
        Assert.Equal(DateTimeKind.Utc, input.FirstSeenAt.Kind);
        Assert.NotNull(input.LastSeenAt);
        Assert.Equal("https://threatfox.example/ioc123", input.ProviderDetailsLink);
        Assert.Equal(new[] { " BotNet ", "botnet " }, input.Tags);
        Assert.Equal("system", input.Metadata["reporter"]);
        Assert.Equal("trickbot", input.Payload["malware"]);
        Assert.Equal(Payload, input.Payload);
    }

    [Fact]
    public void Input_contract_allows_all_optional_fields_to_be_null()
    {
        var input = new ProviderRecordInput(
            ProviderId: 2,
            ProviderRecordId: "phish-9",
            RawIoc: "http://evil.example/x",
            DeclaredType: null,
            FirstSeenAt: DateTime.UtcNow,
            LastSeenAt: null,
            ProviderDetailsLink: null,
            Tags: null,
            Metadata: null,
            Payload: null);

        Assert.Null(input.DeclaredType);
        Assert.Null(input.LastSeenAt);
        Assert.Null(input.ProviderDetailsLink);
        Assert.Null(input.Tags);
        Assert.Null(input.Metadata);
        Assert.Null(input.Payload);
    }

    [Fact]
    public void Output_contract_is_created_by_validation_with_a_normalized_identity()
    {
        var record = ProviderValidationFactory.CreateValidatorWithDefault()
            .Validate(ProviderValidationFactory.ValidInput(), recordIndex: 1)
            .Normalized!;

        Assert.IsType<NormalizationResult>(record.IocIdentity);
        Assert.Equal("api.example.co.uk", record.IocIdentity.IocValue);
        Assert.Equal(IocType.Domain, record.IocIdentity.Type);
        Assert.Equal("example.co.uk", record.IocIdentity.ParentDomain);
    }

    [Fact]
    public void Output_contract_retains_provider_identity_and_opaque_payload()
    {
        var input = ProviderValidationFactory.ValidInput() with { Payload = Payload };
        var record = ProviderValidationFactory.CreateValidatorWithDefault()
            .Validate(input, recordIndex: 1)
            .Normalized!;

        Assert.Equal(1, record.ProviderId);
        Assert.Equal("ioc-123", record.ProviderRecordId);
        Assert.Equal("https://threatfox.example/ioc123", record.ProviderDetailsLink);
        Assert.Equal(new[] { "botnet" }, record.Tags);
        Assert.Equal(Payload, record.Payload);
    }

    [Fact]
    public void Failure_returns_one_based_record_index_and_provider_identity()
    {
        var failure = ProviderRecordFailure.Create(
            recordIndex: 3,
            providerId: 1,
            providerRecordId: "ioc-123",
            code: ProviderRecordFailureCode.InvalidIoc,
            field: "rawIoc",
            message: ProviderRecordErrorMessages.InvalidIoc);

        Assert.Equal(3, failure.RecordIndex);
        Assert.Equal(1, failure.ProviderId);
        Assert.Equal("ioc-123", failure.ProviderRecordId);
        Assert.Equal(ProviderRecordFailureCode.InvalidIoc, failure.Code);
        Assert.Equal("rawIoc", failure.Field);
        Assert.Equal(ProviderRecordErrorMessages.InvalidIoc, failure.Message);
    }

    [Fact]
    public void Failure_truncates_provider_record_id_to_100_characters()
    {
        var failure = ProviderRecordFailure.Create(
            1,
            1,
            new string('x', 150),
            ProviderRecordFailureCode.BlankProviderRecordId,
            "providerRecordId",
            ProviderRecordErrorMessages.BlankProviderRecordId);

        Assert.Equal(100, failure.ProviderRecordId.Length);
        Assert.Equal(new string('x', 100), failure.ProviderRecordId);
        Assert.Equal(ProviderRecordFailure.MaxProviderRecordIdLength, failure.ProviderRecordId.Length);
    }

    [Fact]
    public void Failure_truncates_message_to_512_characters()
    {
        var failure = ProviderRecordFailure.Create(
            1,
            1,
            "ioc-123",
            ProviderRecordFailureCode.ProviderRejected,
            "payload",
            new string('y', 900));

        Assert.Equal(512, failure.Message.Length);
        Assert.Equal(new string('y', 512), failure.Message);
        Assert.Equal(ProviderRecordFailure.MaxMessageLength, failure.Message.Length);
    }

    [Fact]
    public void Failure_does_not_truncate_short_values()
    {
        var failure = ProviderRecordFailure.Create(
            1,
            1,
            "ioc-123",
            ProviderRecordFailureCode.InvalidProviderId,
            "providerId",
            "Provider ID must be positive.");

        Assert.Equal("ioc-123", failure.ProviderRecordId);
        Assert.Equal("Provider ID must be positive.", failure.Message);
    }
}
