using ThreatIntelligence.Application.Admin;

namespace ThreatIntelligence.Application.Abstractions.Persistence;

/// <summary>
/// Write model for admin provider management. Implementations validate the submitted configuration
/// against the domain invariants and never expose provider secrets.
/// </summary>
public interface IAdminProviderRepository
{
    Task<ProviderWriteResult> CreateAsync(ProviderWriteRequest request, CancellationToken cancellationToken);

    Task<ProviderWriteResult> UpdateAsync(int providerId, ProviderWriteRequest request, CancellationToken cancellationToken);

    Task<ProviderWriteResult> DeleteAsync(int providerId, CancellationToken cancellationToken);
}
