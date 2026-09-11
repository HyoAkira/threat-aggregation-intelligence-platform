using System.Text.RegularExpressions;

namespace ThreatIntelligence.Infrastructure.Admin;

/// <summary>
/// Removes credentials, credential-bearing URLs, connection-string passwords, bearer tokens, and
/// provider API keys/tokens from text that reaches the Admin log tail. Redaction is best-effort and
/// intentionally errs toward removing a value.
/// </summary>
public static class LogRedactor
{
    private static readonly Regex CredentialUrl = new(
        @"(?<scheme>[a-z][a-z0-9+.\-]*://)(?<user>[^/\s:@]+):(?<password>[^@\s/]+)@",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ConnectionStringSecret = new(
        @"(?<key>password|pwd)\s*=\s*[^;\s]+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex BearerToken = new(
        @"(?<prefix>\bbearer\s+)[A-Za-z0-9\-._~+/]+=*",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex KeyValueSecret = new(
        @"(?<key>api[_-]?key|apikey|access[_-]?token|token|secret)[""']?\s*[:=]\s*[""']?[^""',;\s}]+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string? Redact(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        var redacted = CredentialUrl.Replace(value, "${scheme}${user}:***@");
        redacted = ConnectionStringSecret.Replace(redacted, "${key}=***");
        redacted = BearerToken.Replace(redacted, "${prefix}***");
        redacted = KeyValueSecret.Replace(redacted, "${key}=***");
        return redacted;
    }
}
