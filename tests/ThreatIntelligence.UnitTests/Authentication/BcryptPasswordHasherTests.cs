using ThreatIntelligence.Infrastructure.Authentication;
using ThreatIntelligence.Infrastructure.Configuration;

namespace ThreatIntelligence.UnitTests.Authentication;

public class BcryptPasswordHasherTests
{
    private readonly BcryptPasswordHasher _hasher = new(new PasswordHashOptions(4));

    [Fact]
    public void Hash_can_be_verified_with_the_original_password()
    {
        var hash = _hasher.HashPassword("correct horse battery staple");

        Assert.True(_hasher.VerifyPassword("correct horse battery staple", hash));
        Assert.False(_hasher.VerifyPassword("incorrect password", hash));
        Assert.StartsWith("$2", hash);
    }

    [Fact]
    public void Invalid_hash_fails_verification_without_throwing()
    {
        Assert.False(_hasher.VerifyPassword("password", "not-a-bcrypt-hash"));
    }

    [Fact]
    public void Unknown_user_hash_performs_dummy_verification_and_fails()
    {
        Assert.False(_hasher.VerifyPassword("password", null));
    }
}
