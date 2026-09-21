using Microsoft.EntityFrameworkCore;
using ThreatIntelligence.Application.Abstractions.Secrets;
using ThreatIntelligence.Infrastructure.Persistence;

namespace ThreatIntelligence.Infrastructure.Secrets;

public sealed class ProviderCredentialResolver(
    IDbContextFactory<ThreatIntelligenceDbContext> contextFactory,
    ISecretStore secretStore) : IProviderCredentialResolver
{
    public async Task<ProviderCredential> ResolveAsync(int providerId, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var provider = await context.Providers.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == providerId, cancellationToken);
        if (provider is null)
        {
            throw new ProviderCredentialMissingException($"Provider {providerId}");
        }

        var name = provider.Name switch
        {
            "ThreatFox" => ManagedSecretNames.ThreatFoxApiKey,
            "PhishTank" => ManagedSecretNames.PhishTankAppKey,
            _ => null
        };

        if (name is null)
        {
            if (provider.RequiresApiKey) throw new ProviderCredentialMissingException(provider.Name);
            return new ProviderCredential(provider.Id, provider.Name, false, null);
        }

        string? value;
        try
        {
            value = await secretStore.ResolveAsync(name, cancellationToken);
        }
        catch (SecretManagementUnavailableException) when (!provider.RequiresApiKey)
        {
            value = null;
        }

        if (provider.RequiresApiKey && string.IsNullOrEmpty(value))
        {
            throw new ProviderCredentialMissingException(provider.Name);
        }

        return new ProviderCredential(provider.Id, provider.Name, provider.RequiresApiKey, value);
    }
}
