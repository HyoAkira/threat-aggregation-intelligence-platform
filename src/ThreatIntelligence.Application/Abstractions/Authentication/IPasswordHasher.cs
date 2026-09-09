namespace ThreatIntelligence.Application.Abstractions.Authentication;

/// <summary>
/// Hashes a plaintext password for storage. The production KDF (PBKDF2/BCrypt) and startup
/// activation are provided by E04; E02 implements the seeding workflow against this abstraction.
/// </summary>
public interface IPasswordHasher
{
    string HashPassword(string password);
}