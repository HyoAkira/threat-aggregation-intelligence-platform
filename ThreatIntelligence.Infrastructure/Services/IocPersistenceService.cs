using Microsoft.EntityFrameworkCore;
using ThreatIntelligence.Domain.Entities;

namespace ThreatIntelligence.Infrastructure.Services;

public class IocPersistenceService
{
    private const int BatchSize = 500;
    private readonly ThreatIntelDbContext _dbContext;

    public IocPersistenceService(ThreatIntelDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task PersistAsync(string providerName, IReadOnlyCollection<ProviderIoc> records, CancellationToken cancellationToken = default)
    {
        var provider = await _dbContext.Providers.SingleOrDefaultAsync(p => p.Name == providerName, cancellationToken)
            ?? throw new InvalidOperationException($"Provider '{providerName}' is not configured.");

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var processed = 0;
            foreach (var record in records)
            {
                await UpsertAsync(provider.Id, record, cancellationToken);

                if (++processed % BatchSize == 0)
                {
                    await _dbContext.SaveChangesAsync(cancellationToken);
                    _dbContext.ChangeTracker.Clear();
                }
            }

            if (processed % BatchSize != 0)
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            _dbContext.ChangeTracker.Clear();
            throw;
        }
    }

    private async Task UpsertAsync(int providerId, ProviderIoc record, CancellationToken cancellationToken)
    {
        var incoming = record.Ioc;
        var byValue = _dbContext.Iocs.Local.SingleOrDefault(i =>
                i.ProviderId == providerId &&
                i.IocType == incoming.IocType &&
                i.IocValueHash.SequenceEqual(incoming.IocValueHash) &&
                i.Port == incoming.Port)
            ?? await _dbContext.Iocs.SingleOrDefaultAsync(i =>
                i.ProviderId == providerId &&
                i.IocType == incoming.IocType &&
                i.IocValueHash == incoming.IocValueHash &&
                i.Port == incoming.Port,
                cancellationToken);
        var byRecordId = _dbContext.Iocs.Local.SingleOrDefault(i =>
                i.ProviderId == providerId && i.ProviderRecordId == incoming.ProviderRecordId)
            ?? await _dbContext.Iocs.SingleOrDefaultAsync(i =>
                i.ProviderId == providerId && i.ProviderRecordId == incoming.ProviderRecordId,
                cancellationToken);

        if (byValue is not null && byRecordId is not null && byValue.Id != byRecordId.Id)
        {
            throw new InvalidDataException("Provider record identity conflicts with its normalized IOC identity.");
        }

        var ioc = byValue ?? byRecordId;
        if (ioc is null)
        {
            ioc = incoming;
            ioc.ProviderId = providerId;
            ioc.CreatedAt = DateTime.UtcNow;
            _dbContext.Iocs.Add(ioc);
        }
        else
        {
            CopyCurrentValues(incoming, ioc);
        }

        ioc.UpdatedAt = DateTime.UtcNow;
        await ReplaceTagsAsync(ioc, record.Tags, cancellationToken);
    }

    private async Task ReplaceTagsAsync(Ioc ioc, IReadOnlyCollection<string> tags, CancellationToken cancellationToken)
    {
        var existingJoins = await _dbContext.IocTags.Where(join => join.IocId == ioc.Id).ToListAsync(cancellationToken);
        _dbContext.IocTags.RemoveRange(existingJoins);

        foreach (var tagName in tags.Select(tag => tag.Trim().ToLowerInvariant()).Where(tag => tag.Length > 0).Distinct(StringComparer.Ordinal))
        {
            var tag = await _dbContext.Tags.SingleOrDefaultAsync(candidate => candidate.Name == tagName, cancellationToken);
            if (tag is null)
            {
                tag = new Tag { Name = tagName };
                _dbContext.Tags.Add(tag);
            }

            ioc.IocTags.Add(new IocTag { Ioc = ioc, Tag = tag });
        }
    }

    private static void CopyCurrentValues(Ioc source, Ioc destination)
    {
        destination.ProviderRecordId = source.ProviderRecordId;
        destination.ProviderDetailsLink = source.ProviderDetailsLink;
        destination.IocValue = source.IocValue;
        destination.IocValueHash = source.IocValueHash;
        destination.IocType = source.IocType;
        destination.Port = source.Port;
        destination.ThreatType = source.ThreatType;
        destination.Malware = source.Malware;
        destination.MalwareAlias = source.MalwareAlias;
        destination.MalwarePrintable = source.MalwarePrintable;
        destination.FirstSeenUtc = source.FirstSeenUtc;
        destination.LastSeenUtc = source.LastSeenUtc;
        destination.IsCompromised = source.IsCompromised;
        destination.Reporter = source.Reporter;
        destination.Anonymous = source.Anonymous;
        destination.IsVerified = source.IsVerified;
        destination.Target = source.Target;
        destination.PhishTankDetails = source.PhishTankDetails;
        destination.PhishTankOnline = source.PhishTankOnline;
        destination.UrlHost = source.UrlHost;
        destination.ParentDomain = source.ParentDomain;
        destination.ThreatFoxConfidenceLevel = source.ThreatFoxConfidenceLevel;
    }
}
