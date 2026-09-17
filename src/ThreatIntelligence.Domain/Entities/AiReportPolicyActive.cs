namespace ThreatIntelligence.Domain.Entities;

public sealed class AiReportPolicyActive
{
    private AiReportPolicyActive()
    {
    }

    public AiReportPolicyActive(int activeVersion, DateTime updatedAt)
    {
        Id = 1;
        ActiveVersion = activeVersion;
        UpdatedAt = EnsureUtc(updatedAt, nameof(updatedAt));
    }

    public int Id { get; private set; }
    public int ActiveVersion { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public byte[] Version { get; private set; } = [];

    public void Update(int activeVersion, DateTime updatedAt)
    {
        ActiveVersion = activeVersion;
        UpdatedAt = EnsureUtc(updatedAt, nameof(updatedAt));
    }

    private static DateTime EnsureUtc(DateTime value, string parameterName) =>
        value.Kind == DateTimeKind.Utc ? value : throw new ArgumentException("Timestamp must be UTC.", parameterName);
}
