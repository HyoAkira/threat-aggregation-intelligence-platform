namespace ThreatIntelligence.Application.Abstractions.Hashing;

/// <summary>
/// Hashes a normalized IOC value for index-safe provider identity.
/// Implementations SHALL return exactly 32 bytes and be deterministic.
/// </summary>
public interface IValueHasher
{
    byte[] Hash(string normalizedValue);
}