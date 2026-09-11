using ThreatIntelligence.Application.ProviderRecords;

namespace ThreatIntelligence.UnitTests.ProviderRecords;

public class ProviderRecordSafetyTests
{
    [Fact]
    public void Provider_rejection_message_is_sanitized()
    {
        const string rawIoc = "secret-indicator.example";
        const string payloadSecret = "Bearer payload-token";
        const string credentialBearingUrl = "https://user:password@example.com/private";
        var input = ProviderValidationFactory.ValidInput() with
        {
            RawIoc = rawIoc,
            Payload = new Dictionary<string, object?> { ["Authorization"] = payloadSecret }
        };
        var validator = ProviderValidationFactory.CreateValidator(
            new SecretLeakingValidator(1, rawIoc, payloadSecret, credentialBearingUrl));

        var result = validator.Validate(input, recordIndex: 1);

        Assert.False(result.IsValid);
        Assert.Equal(ProviderRecordFailureCode.ProviderRejected, result.Failure.Code);
        Assert.Equal("payload", result.Failure.Field);
        Assert.Equal(ProviderRecordErrorMessages.ProviderRejected, result.Failure.Message);
        AssertFailureDoesNotContain(result.Failure, rawIoc, payloadSecret, credentialBearingUrl, "Authorization", "password");
    }

    [Fact]
    public void A_credential_bearing_details_link_is_not_echoed_in_the_failure()
    {
        const string rawIoc = "secret-indicator.example";
        const string credentialBearingUrl = "https://user:password@example.com/private";
        var input = ProviderValidationFactory.ValidInput() with
        {
            RawIoc = rawIoc,
            ProviderDetailsLink = credentialBearingUrl,
            Payload = new Dictionary<string, object?> { ["Authorization"] = "Bearer payload-token" }
        };

        var result = ProviderValidationFactory.CreateValidatorWithDefault().Validate(input, recordIndex: 1);

        Assert.False(result.IsValid);
        Assert.Equal(ProviderRecordFailureCode.InvalidDetailsLink, result.Failure.Code);
        AssertFailureDoesNotContain(result.Failure, rawIoc, credentialBearingUrl, "Authorization", "password", "payload-token");
    }

    [Fact]
    public void Validation_does_not_mutate_the_input_or_invoke_provider_validation_after_a_common_failure()
    {
        var tags = new[] { " BotNet ", "botnet " };
        var payload = new Dictionary<string, object?> { ["Authorization"] = "Bearer payload-token" };
        var input = ProviderValidationFactory.ValidInput() with
        {
            RawIoc = " ",
            Tags = tags,
            Payload = payload
        };
        var providerValidator = new InvocationCountingValidator(1);
        var validator = ProviderValidationFactory.CreateValidator(providerValidator);

        var result = validator.Validate(input, recordIndex: 1);

        Assert.Equal(ProviderRecordFailureCode.BlankRawIoc, result.Failure.Code);
        Assert.Equal(0, providerValidator.Invocations);
        Assert.Equal(new[] { " BotNet ", "botnet " }, tags);
        Assert.Equal("Bearer payload-token", payload["Authorization"]);
        Assert.Same(tags, input.Tags);
        Assert.Same(payload, input.Payload);
    }

    private static void AssertFailureDoesNotContain(ProviderRecordFailure failure, params string[] prohibitedValues)
    {
        var fields = new[] { failure.ProviderRecordId, failure.Field, failure.Message };
        foreach (var prohibitedValue in prohibitedValues)
        {
            Assert.DoesNotContain(fields, field => field.Contains(prohibitedValue, StringComparison.OrdinalIgnoreCase));
        }
    }

    private sealed class SecretLeakingValidator : IProviderValidator
    {
        private readonly string _message;

        public SecretLeakingValidator(int providerId, params string[] secrets)
        {
            ProviderId = providerId;
            _message = string.Join(" ", secrets);
        }

        public int ProviderId { get; }

        public ProviderRejection? Validate(ProviderRecordInput input)
            => new(_message, _message);
    }

    private sealed class InvocationCountingValidator : IProviderValidator
    {
        public InvocationCountingValidator(int providerId)
        {
            ProviderId = providerId;
        }

        public int ProviderId { get; }

        public int Invocations { get; private set; }

        public ProviderRejection? Validate(ProviderRecordInput input)
        {
            Invocations++;
            return null;
        }
    }
}
