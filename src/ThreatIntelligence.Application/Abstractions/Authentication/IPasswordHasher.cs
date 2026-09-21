namespace ThreatIntelligence.Application.Abstractions.Authentication;

/// <summary>
/// Hashes and verifies plaintext passwords without exposing implementation details to the application.
/// </summary>
public interface IPasswordHasher
{
    string HashPassword(string password);

    /// <summary>
    /// Verifies a password against a stored hash. A null hash represents an unknown account and
    /// must still perform comparable KDF work so callers do not expose account existence by timing.
    /// </summary>
    bool VerifyPassword(string password, string? passwordHash);
}
