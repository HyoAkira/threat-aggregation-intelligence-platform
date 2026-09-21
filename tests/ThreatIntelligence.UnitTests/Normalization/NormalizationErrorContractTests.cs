using ThreatIntelligence.Application.Normalization;

namespace ThreatIntelligence.UnitTests.Normalization;

public class NormalizationErrorContractTests
{
    private static readonly Dictionary<NormalizationErrorCode, string> ErrorMessages = new()
    {
        [NormalizationErrorCode.InvalidInput] = "The input does not match any supported IOC type.",
        [NormalizationErrorCode.LeadingZeros] = "IPv4 octet contains ambiguous leading zeros.",
        [NormalizationErrorCode.MissingOctets] = "IPv4 address has fewer than four octets.",
        [NormalizationErrorCode.ExtraOctets] = "IPv4 address has more than four octets.",
        [NormalizationErrorCode.InvalidIpv4Port] = "IPv4 port must be between 1 and 65535.",
        [NormalizationErrorCode.InvalidIpv6] = "The input is not a valid IPv6 address.",
        [NormalizationErrorCode.InvalidIpv6Port] = "IPv6 port must be between 1 and 65535.",
        [NormalizationErrorCode.UnmatchedBrackets] = "IPv6 address has unmatched brackets.",
        [NormalizationErrorCode.UnbracketedPort] = "IPv6 port requires bracketed address syntax.",
        [NormalizationErrorCode.InvalidDomain] = "The input is not a valid domain name.",
        [NormalizationErrorCode.DomainTooLong] = "Domain exceeds the 253-character limit.",
        [NormalizationErrorCode.LabelTooLong] = "Domain label exceeds the 63-character limit.",
        [NormalizationErrorCode.EmptyLabel] = "Domain contains an empty label.",
        [NormalizationErrorCode.TldOnly] = "Domain must not be a top-level domain only.",
        [NormalizationErrorCode.InvalidUrlScheme] = "URL must use http or https scheme.",
        [NormalizationErrorCode.MissingUrlHost] = "URL is missing a host.",
        [NormalizationErrorCode.UrlTooLong] = "Normalized URL exceeds the 2048-character limit.",
        [NormalizationErrorCode.InvalidPort] = "Port must be between 1 and 65535.",
        [NormalizationErrorCode.TypeMismatch] = "Declared IOC type does not match the normalized type.",
        [NormalizationErrorCode.InvalidDomainCharacter] = "Domain label contains invalid characters.",
        [NormalizationErrorCode.HyphenBoundary] = "Domain label must not begin or end with a hyphen.",
        [NormalizationErrorCode.DomainScheme] = "Domain must not contain a scheme.",
        [NormalizationErrorCode.DomainPath] = "Domain must not contain a path.",
        [NormalizationErrorCode.DomainPort] = "Domain must not contain a port.",
        [NormalizationErrorCode.DomainWhitespace] = "Domain must not contain whitespace.",
        [NormalizationErrorCode.NoRegistrableDomain] = "Domain must not be a public suffix only.",
        [NormalizationErrorCode.InvalidUrlHost] = "URL host is not valid.",
        [NormalizationErrorCode.InvalidUrlCharacter] = "URL path or query contains invalid whitespace or control characters."
    };

    [Fact]
    public void Every_error_code_has_a_message()
    {
        var codes = Enum.GetValues<NormalizationErrorCode>();
        foreach (var code in codes)
        {
            Assert.True(ErrorMessages.ContainsKey(code),
                $"Missing message for error code: {code}");
        }
    }

    [Theory]
    [MemberData(nameof(ErrorEntries))]
    public void Error_message_is_not_blank(NormalizationErrorCode code, string message)
    {
        Assert.False(string.IsNullOrWhiteSpace(message),
            $"Error code {code} has a blank message.");
    }

    [Theory]
    [MemberData(nameof(ErrorEntries))]
    public void Error_message_does_not_contain_secrets(NormalizationErrorCode code, string message)
    {
        Assert.True(!message.Contains("password", StringComparison.OrdinalIgnoreCase),
            $"Error code {code} mentions password.");
        Assert.True(!message.Contains("token", StringComparison.OrdinalIgnoreCase),
            $"Error code {code} mentions token.");
        Assert.True(!message.Contains("secret", StringComparison.OrdinalIgnoreCase),
            $"Error code {code} mentions secret.");
        Assert.True(!message.Contains("api_key", StringComparison.OrdinalIgnoreCase),
            $"Error code {code} mentions api_key.");
        Assert.True(!message.Contains("apikey", StringComparison.OrdinalIgnoreCase),
            $"Error code {code} mentions apikey.");
        Assert.True(!message.Contains("authorization", StringComparison.OrdinalIgnoreCase),
            $"Error code {code} mentions authorization.");
        Assert.True(!message.Contains("credential", StringComparison.OrdinalIgnoreCase),
            $"Error code {code} mentions credential.");
    }

    [Theory]
    [MemberData(nameof(ErrorEntries))]
    public void Error_message_does_not_contain_ioc_patterns(NormalizationErrorCode code, string message)
    {
        Assert.True(!message.Contains("://"),
            $"Error code {code} message contains a URL pattern.");
        Assert.True(!System.Text.RegularExpressions.Regex.IsMatch(message,
            @"\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}"),
            $"Error code {code} message contains an IPv4 pattern.");
    }

    [Theory]
    [MemberData(nameof(ErrorEntries))]
    public void Error_message_is_reasonable_length(NormalizationErrorCode code, string message)
    {
        Assert.True(message.Length <= 512,
            $"Error code {code} message exceeds 512 characters: {message.Length}");
    }

    public static IEnumerable<object[]> ErrorEntries()
    {
        foreach (var entry in ErrorMessages)
        {
            yield return [entry.Key, entry.Value];
        }
    }
}
