using ThreatIntelligence.Domain.Entities;
using ThreatIntelligence.Domain.Enums;

namespace ThreatIntelligence.Application.Abstractions.Persistence;

/// <summary>
/// Resolves and reads normalized IOC rows. Hash-based lookups confirm the stored normalized
/// value before a row is treated as a match, so a hash collision is never silently accepted.
/// </summary>
public interface IIocRepository
{
    Task<IoC?> FindByEndpointIdentityAsync(
        int providerId,
        IocType iocType,
        string normalizedValue,
        int? port,
        CancellationToken cancellationToken);

    Task<IoC?> FindByProviderRecordIdAsync(
        int providerId,
        string providerRecordId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<IoC>> GetGroupedAsync(
        IocType iocType,
        string normalizedValue,
        CancellationToken cancellationToken);
}