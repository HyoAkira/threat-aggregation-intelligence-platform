using Microsoft.EntityFrameworkCore;
using ThreatIntelligence.Application.Abstractions.Secrets;
using ThreatIntelligence.Infrastructure.Configuration;
using ThreatIntelligence.Infrastructure.Persistence;

namespace ThreatIntelligence.Infrastructure.Secrets;

public sealed class SecretRotationService(
    IDbContextFactory<ThreatIntelligenceDbContext> contextFactory,
    ISecretProtector protector,
    EncryptionOptions options,
    TimeProvider timeProvider) : ISecretRotationService
{
    public async Task<SecretRotationResult> ReencryptAsync(CancellationToken cancellationToken)
    {
        if (!options.IsAvailable) return new SecretRotationResult(0, 0, false);

        var changed = 0;
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var secrets = await context.EncryptedSecrets
            .Where(secret => secret.KeyVersion != options.KeyVersion)
            .OrderBy(secret => secret.Id)
            .ToListAsync(cancellationToken);

        foreach (var secret in secrets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var plaintext = protector.Unprotect(secret.Name, secret.FormatVersion, secret.KeyVersion,
                secret.Ciphertext, secret.Nonce, secret.AuthenticationTag);
            try
            {
                var replacement = protector.Protect(secret.Name, plaintext);
                secret.Replace(replacement.FormatVersion, replacement.KeyVersion, replacement.Ciphertext,
                    replacement.Nonce, replacement.AuthenticationTag, timeProvider.GetUtcNow().UtcDateTime);
                await context.SaveChangesAsync(cancellationToken);
                changed++;
            }
            finally
            {
                plaintext = string.Empty;
            }
        }

        var remaining = await context.EncryptedSecrets.CountAsync(
            secret => secret.KeyVersion != options.KeyVersion, cancellationToken);
        return new SecretRotationResult(changed, remaining, true);
    }
}
