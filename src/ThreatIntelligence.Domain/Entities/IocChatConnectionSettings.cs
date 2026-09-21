namespace ThreatIntelligence.Domain.Entities;

/// <summary>
/// Admin-managed, non-secret connection settings for IOC chat. This singleton is intentionally
/// separate from report generation so enabling or changing chat never changes report behavior.
/// </summary>
public sealed class IocChatConnectionSettings
{
    private IocChatConnectionSettings()
    {
    }

    public IocChatConnectionSettings(string? baseUrl, string? modelId, bool enabled, DateTime updatedAt)
    {
        Id = 1;
        BaseUrl = baseUrl;
        ModelId = modelId;
        Enabled = enabled;
        UpdatedAt = EnsureUtc(updatedAt, nameof(updatedAt));
    }

    public int Id { get; private set; }
    public string? BaseUrl { get; private set; }
    public string? ModelId { get; private set; }
    public bool Enabled { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public byte[] Version { get; private set; } = [];

    public void Update(string? baseUrl, string? modelId, bool enabled, DateTime updatedAt)
    {
        BaseUrl = baseUrl;
        ModelId = modelId;
        Enabled = enabled;
        UpdatedAt = EnsureUtc(updatedAt, nameof(updatedAt));
    }

    private static DateTime EnsureUtc(DateTime value, string parameterName) =>
        value.Kind == DateTimeKind.Utc
            ? value
            : throw new ArgumentException("Timestamp must be UTC.", parameterName);
}