using ThreatIntelligence.Infrastructure.Admin;

namespace ThreatIntelligence.UnitTests.Admin;

public sealed class LogRedactorTests
{
    [Fact]
    public void Credential_bearing_url_loses_its_password()
    {
        var redacted = LogRedactor.Redact("fetching https://admin:hunter2@internal.example/feed now");

        Assert.DoesNotContain("hunter2", redacted, StringComparison.Ordinal);
        Assert.Contains("https://admin:***@internal.example/feed", redacted, StringComparison.Ordinal);
    }

    [Fact]
    public void Connection_string_password_is_removed()
    {
        var redacted = LogRedactor.Redact("Server=db;User Id=sa;Password=Sup3rSecret;Encrypt=False");

        Assert.DoesNotContain("Sup3rSecret", redacted, StringComparison.Ordinal);
        Assert.Contains("Password=***", redacted, StringComparison.Ordinal);
    }

    [Fact]
    public void Bearer_token_is_removed()
    {
        var redacted = LogRedactor.Redact("Authorization: Bearer eyJhbGciOiJIUzI1NiJ9.payload.sig");

        Assert.DoesNotContain("eyJhbGciOiJIUzI1NiJ9", redacted, StringComparison.Ordinal);
        Assert.Contains("Bearer ***", redacted, StringComparison.Ordinal);
    }

    [Fact]
    public void Provider_api_key_and_quoted_tokens_are_removed()
    {
        var redacted = LogRedactor.Redact("{\"apiKey\":\"abcd1234\",\"token\":\"t-9988\"}");

        Assert.DoesNotContain("abcd1234", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("t-9988", redacted, StringComparison.Ordinal);
    }

    [Fact]
    public void Ordinary_text_is_unchanged()
    {
        const string message = "Fetched 12 records for provider ThreatFox";

        Assert.Equal(message, LogRedactor.Redact(message));
    }
}
