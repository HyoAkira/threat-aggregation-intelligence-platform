namespace ThreatIntelligence.Domain.Entities;

public sealed class EncryptedSecret
{
    private EncryptedSecret()
    {
    }

    public EncryptedSecret(
        string name,
        int formatVersion,
        int keyVersion,
        byte[] ciphertext,
        byte[] nonce,
        byte[] authenticationTag,
        DateTime createdAt,
        DateTime updatedAt)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Secret name is required.", nameof(name));
        if (formatVersion < 1) throw new ArgumentOutOfRangeException(nameof(formatVersion));
        if (keyVersion < 1) throw new ArgumentOutOfRangeException(nameof(keyVersion));
        if (nonce.Length != 12) throw new ArgumentException("AES-GCM nonce must be 12 bytes.", nameof(nonce));
        if (authenticationTag.Length != 16) throw new ArgumentException("AES-GCM tag must be 16 bytes.", nameof(authenticationTag));
        if (ciphertext.Length == 0) throw new ArgumentException("Ciphertext is required.", nameof(ciphertext));

        Name = name;
        FormatVersion = formatVersion;
        KeyVersion = keyVersion;
        Ciphertext = ciphertext.ToArray();
        Nonce = nonce.ToArray();
        AuthenticationTag = authenticationTag.ToArray();
        CreatedAt = EnsureUtc(createdAt, nameof(createdAt));
        UpdatedAt = EnsureUtc(updatedAt, nameof(updatedAt));
    }

    public int Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public int FormatVersion { get; private set; }
    public int KeyVersion { get; private set; }
    public byte[] Ciphertext { get; private set; } = [];
    public byte[] Nonce { get; private set; } = [];
    public byte[] AuthenticationTag { get; private set; } = [];
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public byte[] Version { get; private set; } = [];

    public void Replace(
        int formatVersion,
        int keyVersion,
        byte[] ciphertext,
        byte[] nonce,
        byte[] authenticationTag,
        DateTime updatedAt)
    {
        var replacement = new EncryptedSecret(
            Name, formatVersion, keyVersion, ciphertext, nonce, authenticationTag, CreatedAt, updatedAt);
        FormatVersion = replacement.FormatVersion;
        KeyVersion = replacement.KeyVersion;
        Ciphertext = replacement.Ciphertext;
        Nonce = replacement.Nonce;
        AuthenticationTag = replacement.AuthenticationTag;
        UpdatedAt = replacement.UpdatedAt;
    }

    private static DateTime EnsureUtc(DateTime value, string parameterName) =>
        value.Kind == DateTimeKind.Utc ? value : throw new ArgumentException("Timestamp must be UTC.", parameterName);
}
