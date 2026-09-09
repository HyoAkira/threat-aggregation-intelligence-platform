using ThreatIntelligence.Domain.Entities;
using ThreatIntelligence.Domain.Enums;

namespace ThreatIntelligence.UnitTests.Domain;

public class UserTests
{
    private static readonly DateTime Utc = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Valid_user_is_constructed()
    {
        var user = new User("admin", "hash", UserRole.Admin, Utc);
        Assert.Equal("admin", user.Username);
        Assert.Equal(UserRole.Admin, user.Role);
    }

    [Fact]
    public void Blank_username_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => new User("   ", "hash", UserRole.User, Utc));
    }

    [Fact]
    public void Overlength_username_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => new User(new string('a', 51), "hash", UserRole.User, Utc));
    }

    [Fact]
    public void Blank_passwordHash_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => new User("admin", "", UserRole.User, Utc));
    }

    [Fact]
    public void Overlength_passwordHash_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => new User("admin", new string('b', 257), UserRole.User, Utc));
    }

    [Fact]
    public void Undefined_role_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => new User("admin", "hash", (UserRole)99, Utc));
    }

    [Fact]
    public void Non_utc_createdAt_is_rejected()
    {
        var local = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Local);
        Assert.Throws<ArgumentException>(() => new User("admin", "hash", UserRole.User, local));
    }
}
