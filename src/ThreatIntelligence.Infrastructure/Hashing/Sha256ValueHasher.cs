using System.Security.Cryptography;
using System.Text;
using ThreatIntelligence.Application.Abstractions.Hashing;

namespace ThreatIntelligence.Infrastructure.Hashing;

/// <summary>
/// SHA-256 over the UTF-8 bytes of the normalized value. Deterministic and returns exactly 32 bytes.
/// </summary>
public sealed class Sha256ValueHasher : IValueHasher
{
    public byte[] Hash(string normalizedValue)
    {
        ArgumentNullException.ThrowIfNull(normalizedValue);
        return SHA256.HashData(Encoding.UTF8.GetBytes(normalizedValue));
    }
}