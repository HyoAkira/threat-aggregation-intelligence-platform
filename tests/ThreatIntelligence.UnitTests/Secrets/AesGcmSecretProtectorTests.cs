using System.Security.Cryptography;
using ThreatIntelligence.Application.Abstractions.Secrets;
using ThreatIntelligence.Infrastructure.Configuration;
using ThreatIntelligence.Infrastructure.Secrets;

namespace ThreatIntelligence.UnitTests.Secrets;

public sealed class AesGcmSecretProtectorTests
{
    private static EncryptionOptions Options()
    {
        var key = RandomNumberGenerator.GetBytes(EncryptionOptions.RequiredKeyLength);
        return new EncryptionOptions(key, 7, null, new Dictionary<int, byte[]> { [7] = key });
    }

    [Fact]
    public void Protect_round_trips_and_generates_a_fresh_nonce_for_each_write()
    {
        var protector = new AesGcmSecretProtector(Options());
        var first = protector.Protect(ManagedSecretNames.ThreatFoxApiKey, "fixture-value");
        var second = protector.Protect(ManagedSecretNames.ThreatFoxApiKey, "fixture-value");

        Assert.Equal("fixture-value", protector.Unprotect(
            ManagedSecretNames.ThreatFoxApiKey, first.FormatVersion, first.KeyVersion,
            first.Ciphertext, first.Nonce, first.AuthenticationTag));
        Assert.False(first.Nonce.SequenceEqual(second.Nonce));
        Assert.False(first.Ciphertext.SequenceEqual(second.Ciphertext));
        Assert.Equal(12, first.Nonce.Length);
        Assert.Equal(16, first.AuthenticationTag.Length);
    }

    [Fact]
    public void Tampering_or_reassignment_fails_closed()
    {
        var protector = new AesGcmSecretProtector(Options());
        var payload = protector.Protect(ManagedSecretNames.ThreatFoxApiKey, "fixture-value");
        payload.Ciphertext[0] ^= 1;

        Assert.Throws<SecretResolutionException>(() => protector.Unprotect(
            ManagedSecretNames.ThreatFoxApiKey, payload.FormatVersion, payload.KeyVersion,
            payload.Ciphertext, payload.Nonce, payload.AuthenticationTag));
        Assert.Throws<SecretResolutionException>(() => protector.Unprotect(
            ManagedSecretNames.PhishTankAppKey, payload.FormatVersion, payload.KeyVersion,
            payload.Ciphertext, payload.Nonce, payload.AuthenticationTag));
    }

    [Fact]
    public void Unsupported_identity_is_rejected()
    {
        var protector = new AesGcmSecretProtector(Options());

        Assert.Throws<ArgumentException>(() => protector.Protect("Jwt.Secret", "fixture-value"));
    }
}
