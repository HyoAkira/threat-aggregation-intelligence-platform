using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ThreatIntelligence.Application.BatchWriter;
using ThreatIntelligence.Application.ProviderRecords;
using ThreatIntelligence.Domain.Entities;
using ThreatIntelligence.Domain.Enums;
using ThreatIntelligence.Infrastructure.Persistence;

namespace ThreatIntelligence.Infrastructure.Pipeline;

/// <summary>
/// Shared atomic upsert/deduplication pipeline. Resolves each validated provider record by its
/// provider-native and endpoint identities, upserts the IOC row (preserving creation/first-seen),
/// replaces tag associations by scalar ID, and commits the whole batch in bounded chunks inside a
/// single outer transaction. Any invalid record or identity conflict rolls back every IOC/tag write.
/// </summary>
public sealed class IocBatchWriter(
    ThreatIntelligenceDbContext dbContext,
    IReadOnlyDictionary<int, IIocUpsertFieldsMapper> mappers) : IIocBatchWriter
{
    public const int ChunkSize = 2000;

    public async Task<IocBatchResult> ApplyAsync(
        IReadOnlyList<NormalizedProviderRecord> records,
        int providerId,
        CancellationToken cancellationToken)
    {
        if (records is null || records.Count == 0)
        {
            return new IocBatchResult(0, 0);
        }

        if (!mappers.TryGetValue(providerId, out var mapper))
        {
            throw new IocBatchWriteException($"No upsert mapper registered for provider {providerId}.");
        }

        // A concurrent insert may lose a unique-key race. Under the E05 prefetch=1 consumer that
        // cannot co-occur, but as a safety net we re-resolve once in a fresh transaction so the
        // batch never duplicates a row or fails outright.
        var attempts = 0;
        while (true)
        {
            try
            {
                return await ApplyBatchAsync(records, providerId, mapper, cancellationToken);
            }
            catch (DbUpdateException exception) when (IsUniqueViolation(exception) && attempts == 0)
            {
                attempts++;
            }
        }
    }

    private async Task<IocBatchResult> ApplyBatchAsync(
        IReadOnlyList<NormalizedProviderRecord> records,
        int providerId,
        IIocUpsertFieldsMapper mapper,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var added = 0;
        var updated = 0;
        try
        {
            var tagCache = await LoadTagCacheAsync(cancellationToken);

            for (var i = 0; i < records.Count; i += ChunkSize)
            {
                var chunk = records.Skip(i).Take(ChunkSize).Select(mapper.Map).ToList();
                var (chunkAdded, chunkUpdated) =
                    await ProcessChunkAsync(providerId, chunk, tagCache, cancellationToken);
                added += chunkAdded;
                updated += chunkUpdated;
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            dbContext.ChangeTracker.Clear();
            throw;
        }

        return new IocBatchResult(added, updated);
    }

    private async Task<Dictionary<string, int>> LoadTagCacheAsync(CancellationToken cancellationToken)
    {
        var tags = await dbContext.Tags.AsNoTracking().ToListAsync(cancellationToken);
        var cache = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var tag in tags)
        {
            cache[tag.Name] = tag.Id;
        }

        return cache;
    }

    private async Task<(int Added, int Updated)> ProcessChunkAsync(
        int providerId,
        IReadOnlyList<IocUpsertFields> chunk,
        Dictionary<string, int> tagCache,
        CancellationToken cancellationToken)
    {
        var byRecordId = new Dictionary<string, IoC>(StringComparer.Ordinal);
        var byEndpoint = new Dictionary<string, IoC>(StringComparer.Ordinal);
        var pending = new List<(IoC IoC, IReadOnlyList<string> TagNames, bool IsNew)>();
        var newTags = new List<Tag>();
        var added = 0;
        var updated = 0;

        foreach (var fields in chunk)
        {
            var incomingKey = EndpointKey(fields.IocType, fields.IocValueHash, fields.Port);
            var tagNames = NormalizeTags(fields.Tags);
            EnsureTags(tagNames, tagCache, newTags);

            var resolution = await ResolveRowAsync(providerId, fields, byRecordId, byEndpoint, cancellationToken);
            if (resolution.IsNew)
            {
                var row = CreateIoc(providerId, fields);
                dbContext.Iocs.Add(row);
                added++;
                byRecordId[fields.ProviderRecordId] = row;
                byEndpoint[incomingKey] = row;
                pending.Add((row, tagNames, true));
            }
            else
            {
                resolution.Row!.ApplyProviderUpsert(fields);
                updated++;
                pending.Add((resolution.Row, tagNames, false));
            }
        }

        // Persist new tags and IOC rows so their generated IDs are populated.
        await dbContext.SaveChangesAsync(cancellationToken);
        foreach (var tag in newTags)
        {
            tagCache[tag.Name] = tag.Id;
        }

        // Replace tag associations by scalar ID (never tracked Tag entities), so clearing the
        // change tracker between chunks cannot recreate or duplicate tags.
        var updatedIocIds = pending.Where(p => !p.IsNew).Select(p => p.IoC.Id).ToList();
        if (updatedIocIds.Count > 0)
        {
            var existingTags = await dbContext.IoCTags
                .Where(t => updatedIocIds.Contains(t.IocId))
                .ToListAsync(cancellationToken);
            dbContext.IoCTags.RemoveRange(existingTags);
        }

        foreach (var (ioc, tagNames, isNew) in pending)
        {
            foreach (var name in tagNames)
            {
                dbContext.IoCTags.Add(new IoCTag(ioc.Id, tagCache[name]));
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        dbContext.ChangeTracker.Clear();

        return (added, updated);
    }

    /// <summary>
    /// Resolves an incoming record to an existing or new row. Provider-native identity is
    /// authoritative and resolved first; endpoint identity (value-confirmed) handles records that
    /// changed ID. Identity conflicts reject the whole fetch.
    /// </summary>
    private async Task<(IoC? Row, bool IsNew)> ResolveRowAsync(
        int providerId,
        IocUpsertFields fields,
        Dictionary<string, IoC> byRecordId,
        Dictionary<string, IoC> byEndpoint,
        CancellationToken cancellationToken)
    {
        var incomingKey = EndpointKey(fields.IocType, fields.IocValueHash, fields.Port);

        // Provider-native identity (authoritative).
        if (byRecordId.TryGetValue(fields.ProviderRecordId, out var byId))
        {
            return (byId, false);
        }

        var idRow = await dbContext.Iocs.SingleOrDefaultAsync(
            i => i.ProviderId == providerId && i.ProviderRecordId == fields.ProviderRecordId,
            cancellationToken);
        if (idRow is not null)
        {
            var rowKey = EndpointKey(idRow.IocType, idRow.IocValueHash, idRow.Port);
            if (rowKey != incomingKey && await EndpointIdentityIsTakenElsewhere(providerId, idRow, fields, byEndpoint, cancellationToken))
            {
                throw new IocIdentityConflictException(
                    $"Provider record '{fields.ProviderRecordId}' cannot move to normalized identity " +
                    $"'{fields.IocValue}' because that identity already belongs to another provider row.");
            }

            return (idRow, false);
        }

        // Endpoint identity, confirmed by value equality to guard against hash collisions.
        if (byEndpoint.TryGetValue(incomingKey, out var byEp))
        {
            return (byEp, false);
        }

        var endpointRow = await dbContext.Iocs.SingleOrDefaultAsync(
            i => i.ProviderId == providerId
                && i.IocType == fields.IocType
                && i.IocValueHash == fields.IocValueHash
                && EF.Property<int>(i, "PortKey") == (fields.Port ?? 0),
            cancellationToken);
        if (endpointRow is not null && endpointRow.IocValue == fields.IocValue)
        {
            return (endpointRow, false);
        }

        return (null, true);
    }

    private async Task<bool> EndpointIdentityIsTakenElsewhere(
        int providerId,
        IoC idRow,
        IocUpsertFields fields,
        Dictionary<string, IoC> byEndpoint,
        CancellationToken cancellationToken)
    {
        var incomingKey = EndpointKey(fields.IocType, fields.IocValueHash, fields.Port);
        if (byEndpoint.TryGetValue(incomingKey, out var other) && other.Id != idRow.Id)
        {
            return true;
        }

        return await dbContext.Iocs.AnyAsync(
            i => i.Id != idRow.Id
                && i.ProviderId == providerId
                && i.IocType == fields.IocType
                && i.IocValueHash == fields.IocValueHash
                && EF.Property<int>(i, "PortKey") == (fields.Port ?? 0),
            cancellationToken);
    }

    private IoC CreateIoc(int providerId, IocUpsertFields fields)
    {
        return new IoC(
            providerId: providerId,
            providerRecordId: fields.ProviderRecordId,
            iocValue: fields.IocValue,
            iocValueHash: fields.IocValueHash,
            iocType: fields.IocType,
            firstSeenUtc: fields.FirstSeenUtc,
            createdAt: fields.UpdatedAtUtc,
            updatedAt: fields.UpdatedAtUtc,
            providerDetailsLink: fields.ProviderDetailsLink,
            port: fields.Port,
            lastSeenUtc: fields.LastSeenUtc,
            threatType: fields.ThreatType,
            malware: fields.Malware,
            malwareAlias: fields.MalwareAlias,
            malwarePrintable: fields.MalwarePrintable,
            isCompromised: fields.IsCompromised,
            reporter: fields.Reporter,
            anonymous: fields.Anonymous,
            isVerified: fields.IsVerified,
            target: fields.Target,
            phishTankDetails: fields.PhishTankDetails,
            phishTankOnline: fields.PhishTankOnline,
            urlHost: fields.UrlHost,
            parentDomain: fields.ParentDomain,
            threatFoxConfidenceLevel: fields.ThreatFoxConfidenceLevel);
    }

    private static IReadOnlyList<string> NormalizeTags(IReadOnlyList<string>? tags)
    {
        if (tags is null || tags.Count == 0)
        {
            return Array.Empty<string>();
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<string>(tags.Count);
        foreach (var raw in tags)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            var name = raw.Trim().ToLowerInvariant();
            if (seen.Add(name))
            {
                result.Add(name);
            }
        }

        return result;
    }

    private void EnsureTags(IReadOnlyList<string> tagNames, Dictionary<string, int> tagCache, List<Tag> newTags)
    {
        foreach (var name in tagNames)
        {
            if (tagCache.ContainsKey(name))
            {
                continue;
            }

            var tag = new Tag(name);
            dbContext.Tags.Add(tag);
            newTags.Add(tag);
            tagCache[name] = 0; // placeholder; real ID populated after SaveChanges
        }
    }

    private static string EndpointKey(IocType iocType, byte[] iocValueHash, int? port) =>
        $"{(int)iocType}:{Convert.ToHexString(iocValueHash)}:{port ?? 0}";

    private static bool IsUniqueViolation(DbUpdateException exception)
    {
        for (var ex = (Exception?)exception; ex is not null; ex = ex.InnerException)
        {
            if (ex is SqlException { Number: 2601 or 2627 })
            {
                return true;
            }
        }

        return false;
    }
}
