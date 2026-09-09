namespace ThreatIntelligence.Domain.Entities;

public class Tag
{
    private Tag()
    {
    }

    public Tag(string name)
    {
        var normalized = name?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(normalized))
        {
            throw new ArgumentException("Tag name must not be blank.", nameof(name));
        }

        if (normalized.Length > 100)
        {
            throw new ArgumentException("Tag name must not exceed 100 characters.", nameof(name));
        }

        Name = normalized;
    }

    public int Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
}
