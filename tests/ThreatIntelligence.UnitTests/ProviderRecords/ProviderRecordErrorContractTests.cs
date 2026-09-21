using ThreatIntelligence.Application.ProviderRecords;

namespace ThreatIntelligence.UnitTests.ProviderRecords;

public class ProviderRecordErrorContractTests
{
    private static readonly Dictionary<ProviderRecordFailureCode, string> Messages = new()
    {
        [ProviderRecordFailureCode.InvalidProviderId] = ProviderRecordErrorMessages.InvalidProviderId,
        [ProviderRecordFailureCode.BlankProviderRecordId] = ProviderRecordErrorMessages.BlankProviderRecordId,
        [ProviderRecordFailureCode.ProviderRecordIdTooLong] = ProviderRecordErrorMessages.ProviderRecordIdTooLong,
        [ProviderRecordFailureCode.BlankRawIoc] = ProviderRecordErrorMessages.BlankRawIoc,
        [ProviderRecordFailureCode.InvalidIoc] = ProviderRecordErrorMessages.InvalidIoc,
        [ProviderRecordFailureCode.TypeMismatch] = ProviderRecordErrorMessages.TypeMismatch,
        [ProviderRecordFailureCode.InvalidTimestamp] = ProviderRecordErrorMessages.InvalidTimestamp,
        [ProviderRecordFailureCode.InvalidDetailsLink] = ProviderRecordErrorMessages.InvalidDetailsLink,
        [ProviderRecordFailureCode.DetailsLinkTooLong] = ProviderRecordErrorMessages.DetailsLinkTooLong,
        [ProviderRecordFailureCode.InvalidTag] = ProviderRecordErrorMessages.InvalidTag,
        [ProviderRecordFailureCode.UnsupportedProvider] = ProviderRecordErrorMessages.UnsupportedProvider,
        [ProviderRecordFailureCode.ProviderRejected] = ProviderRecordErrorMessages.ProviderRejected
    };

    [Fact]
    public void Every_failure_code_has_a_message()
    {
        var codes = Enum.GetValues<ProviderRecordFailureCode>();
        foreach (var code in codes)
        {
            Assert.True(Messages.ContainsKey(code), $"Missing message for failure code: {code}");
        }
    }

    [Theory]
    [MemberData(nameof(MessageEntries))]
    public void Message_is_not_blank(ProviderRecordFailureCode code, string message)
    {
        Assert.False(string.IsNullOrWhiteSpace(message), $"Failure code {code} has a blank message.");
    }

    [Theory]
    [MemberData(nameof(MessageEntries))]
    public void Message_respects_length_rule(ProviderRecordFailureCode code, string message)
    {
        Assert.True(message.Length <= 512, $"Failure code {code} message exceeds 512 characters: {message.Length}");
    }

    [Theory]
    [MemberData(nameof(MessageEntries))]
    public void Message_contains_no_secret_terms(ProviderRecordFailureCode code, string message)
    {
        foreach (var secret in new[] { "password", "token", "secret", "api_key", "apikey", "authorization", "credential" })
        {
            Assert.True(!message.Contains(secret, StringComparison.OrdinalIgnoreCase),
                $"Failure code {code} message mentions {secret}.");
        }
    }

    [Theory]
    [MemberData(nameof(MessageEntries))]
    public void Message_contains_no_embedded_ioc(ProviderRecordFailureCode code, string message)
    {
        Assert.True(!message.Contains("://"),
            $"Failure code {code} message contains a URL pattern.");
        Assert.False(System.Text.RegularExpressions.Regex.IsMatch(message,
            @"\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}"),
            $"Failure code {code} message contains an IPv4 pattern.");
    }

    public static IEnumerable<object[]> MessageEntries()
    {
        foreach (var entry in Messages)
        {
            yield return [entry.Key, entry.Value];
        }
    }
}