using ThreatIntelligence.Application.Abstractions.Authentication;
using ThreatIntelligence.Application.Features.Auth.Login;
using ThreatIntelligence.Domain.Entities;
using ThreatIntelligence.Domain.Enums;

namespace ThreatIntelligence.UnitTests.Authentication;

public class LoginHandlerTests
{
    [Fact]
    public async Task Valid_credentials_return_the_issued_token_and_identity()
    {
        var handler = CreateHandler(new User("admin", "hash", UserRole.Admin, DateTime.UtcNow), passwordMatches: true);

        var result = await handler.Handle(new LoginCommand("admin", "password"), CancellationToken.None);

        Assert.Equal("token", result!.Token);
        Assert.Equal("admin", result.Username);
        Assert.Equal("Admin", result.Role);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Unknown_username_and_wrong_password_both_verify_and_have_the_same_result(bool userExists, bool passwordMatches)
    {
        var user = userExists ? new User("admin", "hash", UserRole.Admin, DateTime.UtcNow) : null;
        var hasher = new Hasher(passwordMatches);
        var handler = CreateHandler(user, hasher);

        var result = await handler.Handle(new LoginCommand("admin", "wrong"), CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(1, hasher.VerificationCount);
        Assert.Equal(user?.PasswordHash, hasher.LastPasswordHash);
    }

    private static LoginHandler CreateHandler(User? user, bool passwordMatches) => CreateHandler(user, new Hasher(passwordMatches));

    private static LoginHandler CreateHandler(User? user, Hasher hasher) => new(
        new Reader(user),
        hasher,
        new Tokens());

    private sealed class Reader(User? user) : IUserAuthenticationReader
    {
        public Task<User?> FindByUsernameAsync(string username, CancellationToken cancellationToken) => Task.FromResult(user);
    }

    private sealed class Hasher(bool passwordMatches) : IPasswordHasher
    {
        public int VerificationCount { get; private set; }
        public string? LastPasswordHash { get; private set; }

        public string HashPassword(string password) => password;

        public bool VerifyPassword(string password, string? passwordHash)
        {
            VerificationCount++;
            LastPasswordHash = passwordHash;
            return passwordMatches;
        }
    }

    private sealed class Tokens : ITokenIssuer
    {
        public IssuedToken Issue(User user) => new("token", DateTimeOffset.UtcNow.AddHours(8));
    }
}
