namespace ThreatIntelligence.Application.Abstractions.Secrets;

public interface IProviderCredentialResolver
{
    Task<ProviderCredential> ResolveAsync(int providerId, CancellationToken cancellationToken);
}

public sealed record ProviderCredential(
    int ProviderId,
    string ProviderName,
    bool RequiresApiKey,
    string? Value);

public sealed class ProviderCredentialMissingException(string providerName)
    : Exception($"{providerName} API credential is required but is not configured.");
