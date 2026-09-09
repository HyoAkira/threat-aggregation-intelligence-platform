using Microsoft.EntityFrameworkCore;
using ThreatIntelligence.Application.Abstractions.Hashing;
using ThreatIntelligence.Application.Abstractions.Persistence;
using ThreatIntelligence.Domain.Entities;
using ThreatIntelligence.Domain.Enums;
using ThreatIntelligence.Infrastructure.Hashing;
using ThreatIntelligence.Infrastructure.Persistence;

namespace ThreatIntelligence.Infrastructure.Persistence.Repositories;

public sealed class IocRepository(
    ThreatIntelligenceDbContext dbContext,
    IValueHasher hasher) : IIocRepository
{
    public async Task<IoC?> FindByEndpointIdentityAsync(
        int providerId,
        IocType iocType,
        string normalizedValue,
        int? port,
        CancellationToken cancellationToken)
    {
        var hash = hasher.Hash(normalizedValue);
        var portKey = port ?? 0;

        var candidate = await dbContext.Iocs
            .AsNoTracking()
            .SingleOrDefaultAsync(
                i => i.ProviderId == providerId
                    && i.IocType == iocType
                    && i.IocValueHash == hash
                    && EF.Property<int>(i, "PortKey") == portKey,
                cancellationToken);

        if (candidate is null)
        {
            return null;
        }

        return ValueEquality.Matches(candidate.IocValueHash, candidate.IocValue, normalizedValue, hasher)
            ? candidate
            : null;
    }

    public async Task<IoC?> FindByProviderRecordIdAsync(
        int providerId,
        string providerRecordId,
        CancellationToken cancellationToken)
    {
        return await dbContext.Iocs
            .AsNoTracking()
            .SingleOrDefaultAsync(
                i => i.ProviderId == providerId && i.ProviderRecordId == providerRecordId,
                cancellationToken);
    }

    public async Task<IReadOnlyList<IoC>> GetGroupedAsync(
        IocType iocType,
        string normalizedValue,
        CancellationToken cancellationToken)
    {
        var hash = hasher.Hash(normalizedValue);

        var candidates = await dbContext.Iocs
            .AsNoTracking()
            .Where(i => i.IocType == iocType && i.IocValueHash == hash)
            .ToListAsync(cancellationToken);

        return candidates
            .Where(i => ValueEquality.Matches(i.IocValueHash, i.IocValue, normalizedValue, hasher))
            .ToList();
    }
}