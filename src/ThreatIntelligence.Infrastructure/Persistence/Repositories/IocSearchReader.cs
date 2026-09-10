using Microsoft.EntityFrameworkCore;
using ThreatIntelligence.Application.Abstractions.Hashing;
using ThreatIntelligence.Application.Abstractions.Persistence;
using ThreatIntelligence.Application.Features.Iocs.Queries.SearchIocs;
using ThreatIntelligence.Application.Normalization;
using ThreatIntelligence.Domain.Entities;

namespace ThreatIntelligence.Infrastructure.Persistence.Repositories;

public sealed class IocSearchReader(
    ThreatIntelligenceDbContext dbContext,
    IIocClassifier classifier,
    IValueHasher hasher) : IIocSearchReader
{
    public async Task<IocSearchResult?> SearchExactAsync(
        string query,
        string? tag,
        CancellationToken cancellationToken)
    {
        var normalized = classifier.Classify(query);
        if (!normalized.IsSuccess)
        {
            return null;
        }

        var identity = normalized.OrThrow();
        var hash = hasher.Hash(identity.IocValue);
        var matches = await dbContext.Iocs.AsNoTracking()
            .Where(i => i.IocType == identity.Type
                && i.IocValueHash == hash
                && i.IocValue == identity.IocValue
                && i.Port == identity.Port)
            .ToListAsync(cancellationToken);

        if (tag is not null)
        {
            var normalizedTag = tag.Trim().ToLowerInvariant();
            var taggedIocIds = await (
                from iocTag in dbContext.IoCTags.AsNoTracking()
                join tagRow in dbContext.Tags.AsNoTracking() on iocTag.TagId equals tagRow.Id
                where tagRow.Name == normalizedTag
                select iocTag.IocId).ToListAsync(cancellationToken);
            matches = matches.Where(i => taggedIocIds.Contains(i.Id)).ToList();
        }

        if (matches.Count == 0)
        {
            return null;
        }

        var providerIds = matches.Select(i => i.ProviderId).Distinct().ToList();
        var providers = await dbContext.Providers.AsNoTracking()
            .Where(provider => providerIds.Contains(provider.Id))
            .ToDictionaryAsync(provider => provider.Id, provider => provider.Name, cancellationToken);

        var iocIds = matches.Select(i => i.Id).ToList();
        var tags = await (
            from iocTag in dbContext.IoCTags.AsNoTracking()
            join tagRow in dbContext.Tags.AsNoTracking() on iocTag.TagId equals tagRow.Id
            where iocIds.Contains(iocTag.IocId)
            select new { iocTag.IocId, tagRow.Name }).ToListAsync(cancellationToken);

        return new IocSearchResult(
            identity.IocValue,
            identity.Type,
            identity.UrlHost,
            identity.ParentDomain,
            matches.Min(i => i.FirstSeenUtc),
            matches.Where(i => i.LastSeenUtc.HasValue).Select(i => i.LastSeenUtc).Max(),
            matches.Select(i => new IocSourceResult(
                providers[i.ProviderId],
                i.ProviderRecordId,
                i.ProviderDetailsLink,
                i.Port,
                i.FirstSeenUtc,
                i.LastSeenUtc,
                i.ThreatType,
                i.Malware,
                i.ThreatFoxConfidenceLevel,
                i.IsVerified,
                i.Target,
                i.PhishTankOnline,
                tags.Where(t => t.IocId == i.Id).Select(t => t.Name).ToArray(),
                false)).ToArray());
    }
}
