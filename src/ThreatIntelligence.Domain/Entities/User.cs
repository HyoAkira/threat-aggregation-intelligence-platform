using ThreatIntelligence.Domain.Common;
using ThreatIntelligence.Domain.Enums;

namespace ThreatIntelligence.Domain.Entities;

public class User
{
    private User()
    {
    }

    public User(string username, string passwordHash, UserRole role, DateTime createdAt)
    {
        Username = Guard.Required(username, 50, nameof(username));
        PasswordHash = Guard.Required(passwordHash, 256, nameof(passwordHash));
        Role = Guard.Defined(role, nameof(role));
        CreatedAt = Guard.Utc(createdAt, nameof(createdAt));
    }

    public int Id { get; private set; }
    public string Username { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public UserRole Role { get; private set; }
    public DateTime CreatedAt { get; private set; }
}
