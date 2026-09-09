using ThreatIntelligence.Application.ProviderRecords;
using ThreatIntelligence.Domain.Enums;

namespace ThreatIntelligence.UnitTests.ProviderRecords;

public class ProviderRecordBatchValidationTests
{
    [Fact]
    public void Batch_stops_at_the_first_invalid_record_without_enumerating_later_records()
    {
        var first = ProviderValidationFactory.ValidInput();
        var second = ProviderValidationFactory.ValidInput() with { RawIoc = " " };
        var validator = ProviderValidationFactory.CreateValidatorWithDefault();

        var result = validator.ValidateBatch(Inputs(first, second));

        Assert.False(result.IsValid);
        Assert.Single(result.Records);
        Assert.Equal("ioc-123", result.Records[0].ProviderRecordId);
        Assert.Equal(2, result.Failure.RecordIndex);
        Assert.Equal(ProviderRecordFailureCode.BlankRawIoc, result.Failure.Code);
    }

    [Fact]
    public void Batch_returns_normalized_records_when_every_input_is_valid()
    {
        var first = ProviderValidationFactory.ValidInput();
        var second = ProviderValidationFactory.ValidInput() with
        {
            ProviderRecordId = "ioc-124",
            RawIoc = "1.2.3.4",
            DeclaredType = IocType.Ipv4
        };
        var validator = ProviderValidationFactory.CreateValidatorWithDefault();

        var result = validator.ValidateBatch(new[] { first, second });

        Assert.True(result.IsValid);
        Assert.Null(result.Failure);
        Assert.Equal(2, result.Records.Count);
        Assert.Equal("api.example.co.uk", result.Records[0].IocIdentity.IocValue);
        Assert.Equal("1.2.3.4", result.Records[1].IocIdentity.IocValue);
    }

    [Fact]
    public void Batch_failure_index_is_one_based()
    {
        var inputs = new[]
        {
            ProviderValidationFactory.ValidInput() with { ProviderRecordId = "ioc-1" },
            ProviderValidationFactory.ValidInput() with { ProviderRecordId = "ioc-2" },
            ProviderValidationFactory.ValidInput() with { ProviderRecordId = "ioc-3", RawIoc = " " }
        };

        var result = ProviderValidationFactory.CreateValidatorWithDefault().ValidateBatch(inputs);

        Assert.Equal(3, result.Failure.RecordIndex);
        Assert.Equal("ioc-3", result.Failure.ProviderRecordId);
        Assert.Equal(2, result.Records.Count);
    }

    [Fact]
    public void Within_record_validation_order_is_deterministic()
    {
        var rejecting = new RejectingValidator(1);
        var validator = ProviderValidationFactory.CreateValidator(rejecting);
        var input = ProviderValidationFactory.ValidInput() with
        {
            ProviderId = 0,
            ProviderRecordId = " ",
            RawIoc = " ",
            FirstSeenAt = new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Local),
            ProviderDetailsLink = "ftp://example.com",
            Tags = new[] { new string('x', 101) }
        };

        var result = validator.Validate(input, recordIndex: 1);

        Assert.Equal(ProviderRecordFailureCode.InvalidProviderId, result.Failure.Code);
        Assert.Equal(0, rejecting.Invocations);
    }

    [Fact]
    public void Raw_ioc_presence_fails_before_all_later_validation_steps()
    {
        var rejecting = new RejectingValidator(1);
        var validator = ProviderValidationFactory.CreateValidator(rejecting);
        var input = ProviderValidationFactory.ValidInput() with
        {
            RawIoc = " ",
            FirstSeenAt = new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Local),
            ProviderDetailsLink = "ftp://example.com",
            Tags = new[] { new string('x', 101) }
        };

        var result = validator.Validate(input, recordIndex: 1);

        Assert.Equal(ProviderRecordFailureCode.BlankRawIoc, result.Failure.Code);
        Assert.Equal(0, rejecting.Invocations);
    }

    [Fact]
    public void Ioc_normalization_fails_before_timestamps_links_tags_and_provider_validation()
    {
        var rejecting = new RejectingValidator(1);
        var validator = ProviderValidationFactory.CreateValidator(rejecting);
        var input = ProviderValidationFactory.ValidInput() with
        {
            RawIoc = "foo_bar.example",
            DeclaredType = null,
            FirstSeenAt = new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Local),
            ProviderDetailsLink = "ftp://example.com",
            Tags = new[] { new string('x', 101) }
        };

        var result = validator.Validate(input, recordIndex: 1);

        Assert.Equal(ProviderRecordFailureCode.InvalidIoc, result.Failure.Code);
        Assert.Equal(0, rejecting.Invocations);
    }

    [Fact]
    public void Declared_type_agreement_fails_before_timestamps_links_tags_and_provider_validation()
    {
        var rejecting = new RejectingValidator(1);
        var validator = ProviderValidationFactory.CreateValidator(rejecting);
        var input = ProviderValidationFactory.ValidInput() with
        {
            RawIoc = "http://example.com",
            DeclaredType = IocType.Domain,
            FirstSeenAt = new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Local),
            ProviderDetailsLink = "ftp://example.com",
            Tags = new[] { new string('x', 101) }
        };

        var result = validator.Validate(input, recordIndex: 1);

        Assert.Equal(ProviderRecordFailureCode.TypeMismatch, result.Failure.Code);
        Assert.Equal(0, rejecting.Invocations);
    }

    [Fact]
    public void Timestamps_fail_before_links_tags_and_provider_validation()
    {
        var rejecting = new RejectingValidator(1);
        var validator = ProviderValidationFactory.CreateValidator(rejecting);
        var input = ProviderValidationFactory.ValidInput() with
        {
            FirstSeenAt = new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Local),
            ProviderDetailsLink = "ftp://example.com",
            Tags = new[] { new string('x', 101) }
        };

        var result = validator.Validate(input, recordIndex: 1);

        Assert.Equal(ProviderRecordFailureCode.InvalidTimestamp, result.Failure.Code);
        Assert.Equal("firstSeenAt", result.Failure.Field);
        Assert.Equal(0, rejecting.Invocations);
    }

    [Fact]
    public void Details_links_fail_before_tags_and_provider_validation()
    {
        var rejecting = new RejectingValidator(1);
        var validator = ProviderValidationFactory.CreateValidator(rejecting);
        var input = ProviderValidationFactory.ValidInput() with
        {
            ProviderDetailsLink = "ftp://example.com",
            Tags = new[] { new string('x', 101) }
        };

        var result = validator.Validate(input, recordIndex: 1);

        Assert.Equal(ProviderRecordFailureCode.InvalidDetailsLink, result.Failure.Code);
        Assert.Equal(0, rejecting.Invocations);
    }

    [Fact]
    public void Tags_fail_before_the_provider_validator()
    {
        var rejecting = new RejectingValidator(1);
        var validator = ProviderValidationFactory.CreateValidator(rejecting);
        var input = ProviderValidationFactory.ValidInput() with { Tags = new[] { new string('x', 101) } };

        var result = validator.Validate(input, recordIndex: 1);

        Assert.Equal(ProviderRecordFailureCode.InvalidTag, result.Failure.Code);
        Assert.Equal(0, rejecting.Invocations);
    }

    private static IEnumerable<ProviderRecordInput> Inputs(ProviderRecordInput first, ProviderRecordInput second)
    {
        yield return first;
        yield return second;
        throw new InvalidOperationException("A later record must not be evaluated.");
    }

    private sealed class RejectingValidator : IProviderValidator
    {
        public RejectingValidator(int providerId)
        {
            ProviderId = providerId;
        }

        public int ProviderId { get; }

        public int Invocations { get; private set; }

        public ProviderRejection? Validate(ProviderRecordInput input)
        {
            Invocations++;
            return new ProviderRejection("payload", "Rejected");
        }
    }
}
