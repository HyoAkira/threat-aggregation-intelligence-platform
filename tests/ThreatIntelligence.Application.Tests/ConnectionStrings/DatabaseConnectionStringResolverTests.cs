using ThreatIntelligence.Infrastructure.ConnectionStrings;

namespace ThreatIntelligence.Application.Tests.ConnectionStrings;

public class DatabaseConnectionStringResolverTests
{
    [Fact]
    public void Resolve_WithTokenAndPassword_ReplacesToken()
    {
        const string configured = "Server=localhost,1433;Database=ThreatIntel;User Id=sa;Password=__DB_PASSWORD__;TrustServerCertificate=True;";

        var result = DatabaseConnectionStringResolver.Resolve(configured, "FAKE_PASSWORD_123");

        Assert.Equal(
            "Server=localhost,1433;Database=ThreatIntel;User Id=sa;Password=FAKE_PASSWORD_123;TrustServerCertificate=True;",
            result);
    }

    [Fact]
    public void Resolve_WithNullPassword_LeavesEmptyPassword()
    {
        const string configured = "Password=__DB_PASSWORD__";

        var result = DatabaseConnectionStringResolver.Resolve(configured, null);

        Assert.Equal("Password=", result);
    }

    [Fact]
    public void Resolve_WithoutToken_ReturnsInputUnchanged()
    {
        const string configured = "Server=localhost;Database=ThreatIntel;";

        var result = DatabaseConnectionStringResolver.Resolve(configured, "FAKE_PASSWORD_123");

        Assert.Equal(configured, result);
    }

    [Fact]
    public void Resolve_WithNullConnectionString_ReturnsNull()
    {
        var result = DatabaseConnectionStringResolver.Resolve(null, "FAKE_PASSWORD_123");

        Assert.Null(result);
    }
}