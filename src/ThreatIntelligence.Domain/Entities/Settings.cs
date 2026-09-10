using ThreatIntelligence.Domain.Common;

namespace ThreatIntelligence.Domain.Entities;

public class Settings
{
    private Settings()
    {
    }

    public Settings(string key, string value, DateTime updatedAt)
    {
        Key = Guard.Required(key, 100, nameof(key));
        Value = Guard.Required(value, nameof(value));
        UpdatedAt = Guard.Utc(updatedAt, nameof(updatedAt));
    }

    public int Id { get; private set; }
    public string Key { get; private set; } = string.Empty;
    public string Value { get; private set; } = string.Empty;
    public DateTime UpdatedAt { get; private set; }

    public void Update(string value, DateTime updatedAt)
    {
        Value = Guard.Required(value, nameof(value));
        UpdatedAt = Guard.Utc(updatedAt, nameof(updatedAt));
    }
}
