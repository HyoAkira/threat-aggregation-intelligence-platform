using System.Security.Cryptography;
using System.Text;
using ThreatIntelligence.Application.Abstractions.Hashing;

namespace ThreatIntelligence.Infrastructure.Hashing;

/// <summary>
/// Confirms a hash-lookup candidate by re-hashing the incoming value and comparing the
/// stored normalized value, so a hash collision is never silently treated as a match.
/// </summary>
public static class ValueEquality
{
    public static bool Matches(
        byte[] storedHash,
        string storedValue,
        string incomingValue,
        IValueHasher hasher)
    {
        ArgumentNullException.ThrowIfNull(storedHash);
        ArgumentNullException.ThrowIfNull(storedValue);
        ArgumentNullException.ThrowIfNull(incomingValue);
        ArgumentNullException.ThrowIfNull(hasher);

        var incomingHash = hasher.Hash(incomingValue);
        if (!CryptographicOperations.FixedTimeEquals(storedHash, incomingHash))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(storedValue),
            Encoding.UTF8.GetBytes(incomingValue));
    }
}