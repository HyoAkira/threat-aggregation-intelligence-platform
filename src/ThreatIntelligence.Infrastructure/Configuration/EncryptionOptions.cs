using Microsoft.Extensions.Configuration;

namespace ThreatIntelligence.Infrastructure.Configuration;

public sealed record EncryptionOptions(
    byte[]? RootKey,
    int KeyVersion,
    string? InvalidReason,
    IReadOnlyDictionary<int, byte[]>? KeyRing = null)
{
    public const int RequiredKeyLength = 32;
    public bool IsAvailable => RootKey is { Length: RequiredKeyLength } && KeyVersion > 0 && InvalidReason is null;

    public static EncryptionOptions FromConfiguration(IConfiguration configuration)
    {
        var versionText = configuration["Encryption:KeyVersion"];
        var version = string.IsNullOrWhiteSpace(versionText) ? 1 :
            int.TryParse(versionText, out var parsedVersion) ? parsedVersion : 0;
        var encoded = configuration["Encryption:RootKey"];
        var file = configuration["Encryption:RootKeyFile"];

        if (string.IsNullOrWhiteSpace(encoded) && !string.IsNullOrWhiteSpace(file))
        {
            try
            {
                encoded = File.ReadAllText(file).Trim();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return new EncryptionOptions(null, version, "root key file is unavailable");
            }
        }

        if (version < 1)
        {
            return new EncryptionOptions(null, version, "key version must be positive");
        }

        if (string.IsNullOrWhiteSpace(encoded))
        {
            return new EncryptionOptions(null, version, "root key is not configured");
        }

        try
        {
            var key = Convert.FromBase64String(encoded);
            if (key.Length != RequiredKeyLength)
            {
                return new EncryptionOptions(null, version, "root key must decode to exactly 32 bytes");
            }

            var keyRing = new Dictionary<int, byte[]> { [version] = key };
            foreach (var child in configuration.GetSection("Encryption:PreviousKeys").GetChildren())
            {
                if (!int.TryParse(child.Key, out var previousVersion) || previousVersion < 1) continue;
                try
                {
                    var previousKey = Convert.FromBase64String(child.Value ?? string.Empty);
                    if (previousKey.Length == RequiredKeyLength) keyRing[previousVersion] = previousKey;
                }
                catch (FormatException)
                {
                    // Invalid retired keys do not affect validation of the active key.
                }
            }

            return new EncryptionOptions(key, version, null, keyRing);
        }
        catch (FormatException)
        {
            return new EncryptionOptions(null, version, "root key must be base64");
        }
    }
}
