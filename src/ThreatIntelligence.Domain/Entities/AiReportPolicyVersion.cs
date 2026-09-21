namespace ThreatIntelligence.Domain.Entities;

public sealed class AiReportPolicyVersion
{
    private AiReportPolicyVersion()
    {
    }

    public AiReportPolicyVersion(int version, string text, string? authorUserId, DateTime publishedAt)
    {
        if (version < 1) throw new ArgumentOutOfRangeException(nameof(version));
        if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("Policy text is required.", nameof(text));

        Version = version;
        Text = text;
        AuthorUserId = authorUserId;
        PublishedAt = EnsureUtc(publishedAt, nameof(publishedAt));
    }

    public int Version { get; private set; }
    public string Text { get; private set; } = string.Empty;
    public string? AuthorUserId { get; private set; }
    public DateTime PublishedAt { get; private set; }

    private static DateTime EnsureUtc(DateTime value, string parameterName) =>
        value.Kind == DateTimeKind.Utc ? value : throw new ArgumentException("Timestamp must be UTC.", parameterName);
}
