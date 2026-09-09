using ThreatIntelligence.Application.ProviderRecords;

namespace ThreatIntelligence.UnitTests.ProviderRecords;

public class ProviderValidatorExtensionTests
{
    private static ProviderRecordInput InputFor(int providerId) =>
        ProviderValidationFactory.ValidInput() with { ProviderId = providerId };

    [Fact]
    public void The_validator_registered_for_the_inputs_provider_id_is_the_one_invoked()
    {
        var forTen = new RecordingValidator(10, reject: true);
        var forTwelve = new RecordingValidator(12, reject: true);
        var validator = ProviderValidationFactory.CreateValidator(forTen, forTwelve);

        var result = validator.Validate(InputFor(10), recordIndex: 1);

        Assert.False(result.IsValid);
        Assert.Equal(ProviderRecordFailureCode.ProviderRejected, result.Failure.Code);
        Assert.Equal(ProviderRecordErrorMessages.ProviderRejected, result.Failure.Message);
        Assert.Equal(1, forTen.Invocations);
        Assert.Equal(0, forTwelve.Invocations);
    }

    [Fact]
    public void A_registered_validator_is_invoked_after_common_validation_succeeds()
    {
        var rejecting = new RecordingValidator(10, reject: true);
        var validator = ProviderValidationFactory.CreateValidator(rejecting);

        var result = validator.Validate(InputFor(10), recordIndex: 5);

        Assert.False(result.IsValid);
        Assert.Equal(ProviderRecordFailureCode.ProviderRejected, result.Failure.Code);
        Assert.Equal("payload", result.Failure.Field);
        Assert.Equal(ProviderRecordErrorMessages.ProviderRejected, result.Failure.Message);
        Assert.Equal(5, result.Failure.RecordIndex);
        Assert.Equal(10, result.Failure.ProviderId);
        Assert.Equal(1, rejecting.Invocations);
    }

    [Fact]
    public void Common_validation_runs_before_the_registered_validator_is_invoked()
    {
        var rejecting = new RecordingValidator(10, reject: true);
        var validator = ProviderValidationFactory.CreateValidator(rejecting);

        var input = InputFor(10) with { RawIoc = "   " };
        var result = validator.Validate(input, recordIndex: 1);

        Assert.Equal(ProviderRecordFailureCode.BlankRawIoc, result.Failure.Code);
        Assert.Equal(0, rejecting.Invocations);
    }

    [Fact]
    public void An_accepting_registered_validator_lets_the_record_through()
    {
        var accepting = new RecordingValidator(10);
        var validator = ProviderValidationFactory.CreateValidator(accepting);

        var result = validator.Validate(InputFor(10), recordIndex: 1);

        Assert.True(result.IsValid);
        Assert.Equal(1, accepting.Invocations);
    }

    [Fact]
    public void Missing_provider_validator_fails_closed_with_unsupported_provider()
    {
        var validator = ProviderValidationFactory.CreateValidator(new AcceptAllValidator());

        var result = validator.Validate(InputFor(99), recordIndex: 7);

        Assert.False(result.IsValid);
        Assert.Equal(ProviderRecordFailureCode.UnsupportedProvider, result.Failure.Code);
        Assert.Equal("provider", result.Failure.Field);
        Assert.Equal(7, result.Failure.RecordIndex);
        Assert.Equal(99, result.Failure.ProviderId);
    }

    [Fact]
    public void Duplicate_provider_validator_registration_fails_fast()
    {
        Assert.Throws<ArgumentException>(() =>
            ProviderValidationFactory.CreateValidator(
                new RecordingValidator(10),
                new RecordingValidator(10)));
    }

    private sealed class RecordingValidator : IProviderValidator
    {
        private readonly bool _reject;

        public RecordingValidator(int providerId, bool reject = false)
        {
            ProviderId = providerId;
            _reject = reject;
        }

        public int ProviderId { get; }

        public int Invocations { get; private set; }

        public ProviderRejection? Validate(ProviderRecordInput input)
        {
            Invocations++;
            return _reject ? new ProviderRejection("payload", "TEN ONLY") : null;
        }
    }
}
