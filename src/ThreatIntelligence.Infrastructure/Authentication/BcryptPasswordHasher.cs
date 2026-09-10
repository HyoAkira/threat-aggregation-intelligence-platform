using ThreatIntelligence.Application.Abstractions.Authentication;
using ThreatIntelligence.Infrastructure.Configuration;

namespace ThreatIntelligence.Infrastructure.Authentication;

public sealed class BcryptPasswordHasher : IPasswordHasher
{
    private readonly PasswordHashOptions _options;
    private readonly string _unknownUserHash;

    public BcryptPasswordHasher(PasswordHashOptions options)
    {
        _options = options;
        _unknownUserHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString("N"), options.WorkFactor);
    }

    public string HashPassword(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        return BCrypt.Net.BCrypt.HashPassword(password, _options.WorkFactor);
    }

    public bool VerifyPassword(string password, string? passwordHash)
    {
        if (string.IsNullOrEmpty(password))
        {
            return false;
        }

        try
        {
            return BCrypt.Net.BCrypt.Verify(password, string.IsNullOrEmpty(passwordHash) ? _unknownUserHash : passwordHash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            return false;
        }
    }
}
