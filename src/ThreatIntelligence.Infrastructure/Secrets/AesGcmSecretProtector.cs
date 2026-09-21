using System.Security.Cryptography;
using System.Text;
using ThreatIntelligence.Application.Abstractions.Secrets;
using ThreatIntelligence.Infrastructure.Configuration;

namespace ThreatIntelligence.Infrastructure.Secrets;

public sealed class AesGcmSecretProtector(EncryptionOptions options) : ISecretProtector
{
    public const int FormatVersion = 1;
    public const int NonceLength = 12;
    public const int TagLength = 16;

    public EncryptedSecretPayload Protect(string name, string value)
    {
        EnsureSupported(name);
        if (!options.IsAvailable)
        {
            throw new SecretManagementUnavailableException("Secret management is unavailable.");
        }

        var plaintext = Encoding.UTF8.GetBytes(value);
        var nonce = RandomNumberGenerator.GetBytes(NonceLength);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagLength];
        try
        {
            using var aes = new AesGcm(options.RootKey!, TagLength);
            aes.Encrypt(nonce, plaintext, ciphertext, tag, BuildAssociatedData(name));
            return new EncryptedSecretPayload(FormatVersion, options.KeyVersion, ciphertext, nonce, tag);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    public string Unprotect(string name, int formatVersion, int keyVersion, byte[] ciphertext, byte[] nonce, byte[] tag)
    {
        EnsureSupported(name);
        if (!options.IsAvailable || formatVersion != FormatVersion ||
            nonce.Length != NonceLength || tag.Length != TagLength)
        {
            throw new SecretResolutionException("Secret resolution failed.");
        }

        var plaintext = new byte[ciphertext.Length];
        try
        {
            if (options.KeyRing is null || !options.KeyRing.TryGetValue(keyVersion, out var key))
            {
                throw new SecretResolutionException("Secret resolution failed.");
            }

            using var aes = new AesGcm(key, TagLength);
            aes.Decrypt(nonce, ciphertext, tag, plaintext, BuildAssociatedData(name));
            return Encoding.UTF8.GetString(plaintext);
        }
        catch (CryptographicException exception)
        {
            throw new SecretResolutionException("Secret resolution failed.", exception);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    private static byte[] BuildAssociatedData(string name) =>
        Encoding.UTF8.GetBytes($"aes-gcm-v{FormatVersion}\0{name}");

    private static void EnsureSupported(string name)
    {
        if (!ManagedSecretNames.IsSupported(name))
        {
            throw new ArgumentException("The secret identity is not supported.", nameof(name));
        }
    }
}

public interface ISecretProtector
{
    EncryptedSecretPayload Protect(string name, string value);

    string Unprotect(string name, int formatVersion, int keyVersion, byte[] ciphertext, byte[] nonce, byte[] tag);
}

public sealed record EncryptedSecretPayload(
    int FormatVersion,
    int KeyVersion,
    byte[] Ciphertext,
    byte[] Nonce,
    byte[] AuthenticationTag);
