using Microsoft.EntityFrameworkCore;
using ThreatIntelligence.Application.Abstractions.Secrets;
using ThreatIntelligence.Domain.Entities;
using ThreatIntelligence.Infrastructure.Persistence;
using ThreatIntelligence.Infrastructure.Configuration;

namespace ThreatIntelligence.Infrastructure.Secrets;

public sealed class EncryptedSecretStore(
    IDbContextFactory<ThreatIntelligenceDbContext> contextFactory,
    ISecretProtector protector,
    EncryptionOptions encryptionOptions,
    TimeProvider timeProvider) : ISecretStore
{
    private const int MaxValueLength = 4096;

    public async Task<ManagedSecretStatus?> GetStatusAsync(string name, CancellationToken cancellationToken)
    {
        EnsureSupported(name);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var secret = await context.EncryptedSecrets.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Name == name, cancellationToken);
        return secret is null ? null : ToStatus(secret);
    }

    public async Task<string?> ResolveAsync(string name, CancellationToken cancellationToken)
    {
        EnsureSupported(name);
        if (!encryptionOptions.IsAvailable)
        {
            throw new SecretManagementUnavailableException("Secret management is unavailable.");
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var secret = await context.EncryptedSecrets.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Name == name, cancellationToken);
        return secret is null
            ? null
            : protector.Unprotect(secret.Name, secret.FormatVersion, secret.KeyVersion,
                secret.Ciphertext, secret.Nonce, secret.AuthenticationTag);
    }

    public async Task<SecretMutationResult> SetAsync(string name, string value, CancellationToken cancellationToken)
    {
        if (!ManagedSecretNames.IsSupported(name))
        {
            return new SecretMutationResult(SecretMutationStatus.InvalidIdentity, null);
        }

        if (string.IsNullOrWhiteSpace(value) || value.Length > MaxValueLength)
        {
            return new SecretMutationResult(SecretMutationStatus.InvalidValue, null);
        }

        EncryptedSecretPayload payload;
        try
        {
            payload = protector.Protect(name, value);
        }
        catch (SecretManagementUnavailableException)
        {
            return new SecretMutationResult(SecretMutationStatus.Unavailable, null);
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var existing = await context.EncryptedSecrets.SingleOrDefaultAsync(item => item.Name == name, cancellationToken);
        if (existing is null)
        {
            context.EncryptedSecrets.Add(new EncryptedSecret(
                name, payload.FormatVersion, payload.KeyVersion, payload.Ciphertext, payload.Nonce,
                payload.AuthenticationTag, now, now));
        }
        else
        {
            existing.Replace(payload.FormatVersion, payload.KeyVersion, payload.Ciphertext, payload.Nonce,
                payload.AuthenticationTag, now);
        }

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new SecretMutationResult(
            SecretMutationStatus.Success,
            new ManagedSecretStatus(name, true, payload.KeyVersion, now));
    }

    public async Task<bool> ClearAsync(string name, CancellationToken cancellationToken)
    {
        EnsureSupported(name);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await context.EncryptedSecrets.SingleOrDefaultAsync(item => item.Name == name, cancellationToken);
        if (existing is null) return false;
        context.EncryptedSecrets.Remove(existing);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static ManagedSecretStatus ToStatus(EncryptedSecret secret) =>
        new(secret.Name, true, secret.KeyVersion, secret.UpdatedAt);

    private static void EnsureSupported(string name)
    {
        if (!ManagedSecretNames.IsSupported(name))
        {
            throw new ArgumentException("The secret identity is not supported.", nameof(name));
        }
    }
}
