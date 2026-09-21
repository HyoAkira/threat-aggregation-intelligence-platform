using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ThreatIntelligence.Application.Abstractions.Hashing;
using ThreatIntelligence.Application.Abstractions.Persistence;
using ThreatIntelligence.Application.BatchWriter;
using ThreatIntelligence.Application.Features.Iocs.Queries;
using ThreatIntelligence.Application.Normalization;
using ThreatIntelligence.Domain.Entities;
using ThreatIntelligence.Domain.Enums;

namespace ThreatIntelligence.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of the shared IOC read model. Every endpoint resolves canonical group
/// keys in one query and hydrates the page's rows, provider names, and tag names in bounded batched
/// queries so no provider row is ever flattened or lost.
/// </summary>
public sealed class IocQueryReader(
    ThreatIntelligenceDbContext dbContext,
    IValueHasher hasher,
    FalsePositiveEvaluator falsePositiveEvaluator) : IIocQueryReader
{
    public async Task<IReadOnlyList<string>> GetProviderNamesAsync(
        CancellationToken cancellationToken) =>
        await dbContext.Providers.AsNoTracking()
            .OrderBy(provider => provider.Name)
            .Select(provider => provider.Name)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<IocTagCatalogItem>> GetTagCatalogAsync(
        string? query,
        int limit,
        CancellationToken cancellationToken)
    {
        var prefix = query?.Trim().ToLowerInvariant();
        var tags = dbContext.Tags.AsNoTracking().AsQueryable();
        if (!string.IsNullOrEmpty(prefix))
        {
            tags = tags.Where(tag => tag.Name.StartsWith(prefix));
        }

        var matchingTags = await tags
            .Select(tag => new { tag.Id, tag.Name })
            .ToListAsync(cancellationToken);

        var usageByTag = await dbContext.IoCTags.AsNoTracking()
            .Where(iocTag => matchingTags.Select(tag => tag.Id).Contains(iocTag.TagId))
            .GroupBy(iocTag => iocTag.TagId)
            .Select(group => new { TagId = group.Key, UsageCount = group.Count() })
            .ToDictionaryAsync(item => item.TagId, item => item.UsageCount, cancellationToken);

        return matchingTags
            .Select(tag => new IocTagCatalogItem(
                tag.Name,
                usageByTag.GetValueOrDefault(tag.Id)))
            .OrderByDescending(tag => tag.UsageCount)
            .ThenBy(tag => tag.Name, StringComparer.Ordinal)
            .Take(limit)
            .ToList();
    }

    public async Task<PagedResult<IocGroupResult>> SearchGroupsAsync(
        NormalizationResult identity,
        IocSourceFilter filter,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var hash = hasher.Hash(identity.IocValue);
        var query = dbContext.Iocs.AsNoTracking().AsQueryable();

        query = identity.Type == IocType.Domain
            ? query.Where(i =>
                (i.IocType == IocType.Domain
                    && i.IocValueHash == hash
                    && i.IocValue == identity.IocValue)
                || i.UrlHost == identity.IocValue
                || i.ParentDomain == identity.IocValue)
            : query.Where(i => i.IocType == identity.Type
                && i.IocValueHash == hash
                && i.IocValue == identity.IocValue);

        // A bare IP query groups every explicit port and a URL query keeps its canonical port.
        // Domain relation searches must not discard matching URLs that use a non-default port.
        if (identity.Type == IocType.Url || (IsIp(identity.Type) && identity.Port is not null))
        {
            query = query.Where(i => i.Port == identity.Port);
        }

        if (!string.IsNullOrWhiteSpace(filter.Provider))
        {
            var providerIds = await ResolveProviderIdsAsync(filter.Provider, cancellationToken);
            if (providerIds is null)
            {
                return PagedResult<IocGroupResult>.Empty(page, pageSize);
            }

            query = query.Where(i => providerIds.Contains(i.ProviderId));
        }

        if (!string.IsNullOrWhiteSpace(filter.Tag))
        {
            var tagName = filter.Tag.Trim().ToLowerInvariant();
            var tagId = await dbContext.Tags.AsNoTracking()
                .Where(tag => tag.Name == tagName)
                .Select(tag => (int?)tag.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (tagId is null)
            {
                return PagedResult<IocGroupResult>.Empty(page, pageSize);
            }

            query = query.Where(i => dbContext.IoCTags.Any(it => it.IocId == i.Id && it.TagId == tagId));
        }

        if (!string.IsNullOrWhiteSpace(filter.Malware))
        {
            var malwareKey = filter.Malware.Trim().ToLowerInvariant();
            query = query.Where(i => EF.Property<string>(i, "MalwareKey") == malwareKey);
        }

        if (!string.IsNullOrWhiteSpace(filter.Target))
        {
            var targetKey = filter.Target.Trim().ToLowerInvariant();
            query = query.Where(i => EF.Property<string>(i, "TargetKey") == targetKey);
        }

        var grouped = query.GroupBy(i => new { i.IocType, i.IocValue });
        var totalCount = await grouped.CountAsync(cancellationToken);
        if (totalCount == 0)
        {
            return PagedResult<IocGroupResult>.Empty(page, pageSize);
        }

        var pageKeyRows = await grouped
            .OrderByDescending(g => g.Key.IocType == identity.Type && g.Key.IocValue == identity.IocValue)
            .ThenBy(g => g.Max(i => i.LastSeenUtc) == null ? 1 : 0)
            .ThenByDescending(g => g.Max(i => i.LastSeenUtc))
            .ThenBy(g => g.Key.IocType)
            .ThenBy(g => g.Key.IocValue)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(g => new { g.Key.IocType, g.Key.IocValue })
            .ToListAsync(cancellationToken);

        var pageKeys = pageKeyRows.Select(row => new GroupKey(row.IocType, row.IocValue)).ToList();
        var pageValues = pageKeys.Select(key => key.IocValue).Distinct().ToList();
        var rows = await query.Where(i => pageValues.Contains(i.IocValue)).ToListAsync(cancellationToken);
        var wanted = pageKeys.ToHashSet();
        rows = rows.Where(row => wanted.Contains(new GroupKey(row.IocType, row.IocValue))).ToList();
        var groups = await BuildGroupsAsync(rows, cancellationToken);
        var items = pageKeys
            .Where(key => groups.ContainsKey((key.IocType, key.IocValue)))
            .Select(key => groups[(key.IocType, key.IocValue)])
            .ToList();
        return new PagedResult<IocGroupResult>(items, page, pageSize, totalCount);
    }

    public async Task<IReadOnlyDictionary<NormalizationResult, PagedResult<IocGroupResult>>> SearchBatchAsync(
        IReadOnlyList<NormalizationResult> identities,
        IocSourceFilter filter,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        if (identities.Count == 0)
        {
            return new Dictionary<NormalizationResult, PagedResult<IocGroupResult>>();
        }

        var distinct = identities.Distinct().ToList();
        var values = distinct.Select(identity => identity.IocValue).Distinct().ToList();
        var domainValues = distinct
            .Where(identity => identity.Type == IocType.Domain)
            .Select(identity => identity.IocValue)
            .Distinct()
            .ToList();

        // One bounded candidate query covers exact identities and domain relation rows; the precise
        // per-identity predicate is applied in memory so ports, hashes, and relations stay identical
        // to the single-search path.
        var query = dbContext.Iocs.AsNoTracking().Where(i =>
            values.Contains(i.IocValue)
            || domainValues.Contains(i.UrlHost!)
            || domainValues.Contains(i.ParentDomain!));

        if (!string.IsNullOrWhiteSpace(filter.Provider))
        {
            var providerIds = await ResolveProviderIdsAsync(filter.Provider, cancellationToken);
            if (providerIds is null)
            {
                return EmptyBatch(distinct, page, pageSize);
            }

            query = query.Where(i => providerIds.Contains(i.ProviderId));
        }

        if (!string.IsNullOrWhiteSpace(filter.Tag))
        {
            var tagName = filter.Tag.Trim().ToLowerInvariant();
            var tagId = await dbContext.Tags.AsNoTracking()
                .Where(tag => tag.Name == tagName)
                .Select(tag => (int?)tag.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (tagId is null)
            {
                return EmptyBatch(distinct, page, pageSize);
            }

            query = query.Where(i => dbContext.IoCTags.Any(it => it.IocId == i.Id && it.TagId == tagId));
        }

        if (!string.IsNullOrWhiteSpace(filter.Malware))
        {
            var malwareKey = filter.Malware.Trim().ToLowerInvariant();
            query = query.Where(i => EF.Property<string>(i, "MalwareKey") == malwareKey);
        }

        if (!string.IsNullOrWhiteSpace(filter.Target))
        {
            var targetKey = filter.Target.Trim().ToLowerInvariant();
            query = query.Where(i => EF.Property<string>(i, "TargetKey") == targetKey);
        }

        var rows = await query.ToListAsync(cancellationToken);
        if (rows.Count == 0)
        {
            return EmptyBatch(distinct, page, pageSize);
        }

        var groups = await BuildGroupsAsync(rows, cancellationToken);
        var rowsByKey = rows
            .GroupBy(row => (row.IocType, row.IocValue))
            .ToDictionary(group => group.Key, group => group.ToList());

        var results = new Dictionary<NormalizationResult, PagedResult<IocGroupResult>>();
        foreach (var identity in distinct)
        {
            var hash = hasher.Hash(identity.IocValue);
            var matchingKeys = rowsByKey
                .Where(pair => pair.Value.Any(row => MatchesIdentity(identity, hash, row)))
                .Select(pair => pair.Key)
                .ToList();

            if (matchingKeys.Count == 0)
            {
                results[identity] = PagedResult<IocGroupResult>.Empty(page, pageSize);
                continue;
            }

            // Same deterministic ordering as SearchGroupsAsync, expressed over the grouped rows.
            var ordered = matchingKeys
                .OrderByDescending(key => key.IocType == identity.Type && key.IocValue == identity.IocValue)
                .ThenBy(key => groups[key].LastSeenUtc is null ? 1 : 0)
                .ThenByDescending(key => groups[key].LastSeenUtc)
                .ThenBy(key => key.IocType)
                .ThenBy(key => key.IocValue, StringComparer.Ordinal)
                .Select(key => groups[key])
                .ToList();

            var pageItems = ordered
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();
            results[identity] = new PagedResult<IocGroupResult>(pageItems, page, pageSize, ordered.Count);
        }

        return results;
    }

    public async Task<IReadOnlyList<IocSuggestionItem>> GetIocSuggestionsAsync(
        string prefix,
        int limit,
        CancellationToken cancellationToken)
    {
        var normalized = prefix.Trim().ToLowerInvariant();
        var candidates = await dbContext.Iocs.AsNoTracking()
            .Where(i => i.IocValue.ToLower().StartsWith(normalized))
            .Select(i => new { i.IocType, i.IocValue })
            .Distinct()
            .ToListAsync(cancellationToken);

        return candidates
            .OrderBy(candidate => candidate.IocValue, StringComparer.Ordinal)
            .ThenBy(candidate => IocTypeToken.ToToken(candidate.IocType), StringComparer.Ordinal)
            .Take(limit)
            .Select(candidate => new IocSuggestionItem(
                candidate.IocValue,
                IocTypeToken.ToToken(candidate.IocType)))
            .ToList();
    }

    private static IReadOnlyDictionary<NormalizationResult, PagedResult<IocGroupResult>> EmptyBatch(
        IReadOnlyList<NormalizationResult> identities,
        int page,
        int pageSize) =>
        identities.ToDictionary(
            identity => identity,
            _ => PagedResult<IocGroupResult>.Empty(page, pageSize));

    private static bool MatchesIdentity(NormalizationResult identity, byte[] hash, IoC row)
    {
        if (identity.Type == IocType.Domain)
        {
            return (row.IocType == IocType.Domain
                    && row.IocValue == identity.IocValue
                    && row.IocValueHash.AsSpan().SequenceEqual(hash))
                || row.UrlHost == identity.IocValue
                || row.ParentDomain == identity.IocValue;
        }

        if (row.IocType != identity.Type
            || row.IocValue != identity.IocValue
            || !row.IocValueHash.AsSpan().SequenceEqual(hash))
        {
            return false;
        }

        // A bare IP query groups every explicit port and a URL query keeps its canonical port.
        // Domain relation searches must not discard matching URLs that use a non-default port.
        return identity.Type == IocType.Url || (IsIp(identity.Type) && identity.Port is not null)
            ? row.Port == identity.Port
            : true;
    }

    public async Task<IocGroupResult?> GetDetailGroupAsync(int id, CancellationToken cancellationToken)
    {
        var seed = await dbContext.Iocs.AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (seed is null)
        {
            return null;
        }

        var rows = await dbContext.Iocs.AsNoTracking()
            .Where(i => i.IocType == seed.IocType
                && i.IocValueHash == seed.IocValueHash
                && i.IocValue == seed.IocValue)
            .ToListAsync(cancellationToken);

        var groups = await BuildGroupsAsync(rows, cancellationToken);
        return groups[(seed.IocType, seed.IocValue)];
    }

    public async Task<PagedResult<IocGroupResult>> BrowseAsync(
        IocBrowseCriteria criteria,
        CancellationToken cancellationToken)
    {
        var baseQuery = dbContext.Iocs.AsNoTracking().AsQueryable();
        if (criteria.Types is { Count: > 0 } types)
        {
            baseQuery = baseQuery.Where(i => types.Contains(i.IocType));
        }
        else if (criteria.Type is IocType type)
        {
            baseQuery = baseQuery.Where(i => i.IocType == type);
        }

        if (!string.IsNullOrWhiteSpace(criteria.Provider))
        {
            var providerIds = await ResolveProviderIdsAsync(criteria.Provider, cancellationToken);
            if (providerIds is null)
            {
                return PagedResult<IocGroupResult>.Empty(criteria.Page, criteria.PageSize);
            }

            baseQuery = baseQuery.Where(i => providerIds.Contains(i.ProviderId));
        }

        if (!string.IsNullOrWhiteSpace(criteria.Tag))
        {
            var tagNames = criteria.Tag
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(tag => tag.ToLowerInvariant())
                .Distinct()
                .ToArray();
            var tagIds = await dbContext.Tags.AsNoTracking()
                .Where(tag => tagNames.Contains(tag.Name))
                .Select(tag => tag.Id)
                .ToArrayAsync(cancellationToken);
            if (tagIds.Length == 0)
            {
                return PagedResult<IocGroupResult>.Empty(criteria.Page, criteria.PageSize);
            }

            // Multiple tag values are an OR filter: a row matching any selected tag qualifies.
            baseQuery = baseQuery.Where(i => dbContext.IoCTags.Any(it =>
                it.IocId == i.Id && tagIds.Contains(it.TagId)));
        }

        if (!string.IsNullOrWhiteSpace(criteria.Malware))
        {
            var malwareKey = criteria.Malware.Trim().ToLowerInvariant();
            baseQuery = baseQuery.Where(i => EF.Property<string>(i, "MalwareKey") == malwareKey);
        }

        if (!string.IsNullOrWhiteSpace(criteria.Target))
        {
            var targetKey = criteria.Target.Trim().ToLowerInvariant();
            baseQuery = baseQuery.Where(i => EF.Property<string>(i, "TargetKey") == targetKey);
        }

        var grouped = baseQuery.GroupBy(i => new { i.IocType, i.IocValue });

        if (criteria.FirstSeenFrom is DateTime firstFrom)
        {
            grouped = grouped.Where(g => g.Min(i => i.FirstSeenUtc) >= firstFrom);
        }

        if (criteria.FirstSeenTo is DateTime firstTo)
        {
            grouped = grouped.Where(g => g.Min(i => i.FirstSeenUtc) <= firstTo);
        }

        if (criteria.LastSeenFrom is DateTime lastFrom)
        {
            grouped = grouped.Where(g => g.Max(i => i.LastSeenUtc) >= lastFrom);
        }

        if (criteria.LastSeenTo is DateTime lastTo)
        {
            grouped = grouped.Where(g => g.Max(i => i.LastSeenUtc) <= lastTo);
        }

        var totalCount = await grouped.CountAsync(cancellationToken);
        if (totalCount == 0)
        {
            return PagedResult<IocGroupResult>.Empty(criteria.Page, criteria.PageSize);
        }

        // SQL Server has no NULLS LAST; the CASE emulation keeps an all-null aggregate last.
        var ordered = criteria.OrderBy switch
        {
            IocBrowseOrderBy.FirstSeenUtc => criteria.Descending
                ? grouped.OrderByDescending(g => g.Min(i => i.FirstSeenUtc))
                : grouped.OrderBy(g => g.Min(i => i.FirstSeenUtc)),
            IocBrowseOrderBy.IocType => criteria.Descending
                ? grouped.OrderByDescending(g => g.Key.IocType)
                : grouped.OrderBy(g => g.Key.IocType),
            IocBrowseOrderBy.IocValue => criteria.Descending
                ? grouped.OrderByDescending(g => g.Key.IocValue)
                : grouped.OrderBy(g => g.Key.IocValue),
            _ => criteria.Descending
                ? grouped.OrderBy(g => g.Max(i => i.LastSeenUtc) == null ? 1 : 0)
                    .ThenByDescending(g => g.Max(i => i.LastSeenUtc))
                : grouped.OrderBy(g => g.Max(i => i.LastSeenUtc) == null ? 1 : 0)
                    .ThenBy(g => g.Max(i => i.LastSeenUtc))
        };
        var fullyOrdered = ordered.ThenBy(g => g.Key.IocType).ThenBy(g => g.Key.IocValue);

        var pageKeyRows = await fullyOrdered
            .Skip((criteria.Page - 1) * criteria.PageSize)
            .Take(criteria.PageSize)
            .Select(g => new { g.Key.IocType, g.Key.IocValue })
            .ToListAsync(cancellationToken);

        var pageKeys = pageKeyRows.Select(row => new GroupKey(row.IocType, row.IocValue)).ToList();
        var items = await HydrateKeysAsync(pageKeys, cancellationToken);
        return new PagedResult<IocGroupResult>(items, criteria.Page, criteria.PageSize, totalCount);
    }

    public async Task<PagedResult<IocGroupResult>?> GetRelatedAsync(
        int id,
        IocRelationKind relation,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var seed = await dbContext.Iocs.AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (seed is null)
        {
            return null;
        }

        if (relation is IocRelationKind.UrlHost or IocRelationKind.ParentDomain)
        {
            return await GetHostRelatedAsync(seed, relation, page, pageSize, cancellationToken);
        }

        return await GetMetadataRelatedAsync(seed, relation, page, pageSize, cancellationToken);
    }

    private async Task<PagedResult<IocGroupResult>> GetHostRelatedAsync(
        IoC seed,
        IocRelationKind relation,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        if (IsIp(seed.IocType))
        {
            throw new IocRelationNotSupportedException(
                IocErrorCodes.RelationNotSupported,
                "IP IOC groups store no host or parent domain and have no host relations.");
        }

        var target = relation == IocRelationKind.UrlHost ? seed.UrlHost : seed.ParentDomain;
        if (string.IsNullOrEmpty(target))
        {
            return PagedResult<IocGroupResult>.Empty(page, pageSize);
        }

        var relatedRows = relation == IocRelationKind.UrlHost
            ? dbContext.Iocs.AsNoTracking().Where(i => i.UrlHost == target)
            : dbContext.Iocs.AsNoTracking().Where(i => i.ParentDomain == target);

        // Exclude the resolved group itself; a group is identified by its normalized identity.
        relatedRows = relatedRows.Where(i => !(i.IocType == seed.IocType && i.IocValue == seed.IocValue));
        return await PageGroupsAsync(relatedRows, page, pageSize, cancellationToken);
    }

    /// <summary>
    /// Malware/target relations resolve the complete distinct non-empty normalized value set of the
    /// seed group's sources and match any group whose sources carry one of those values. Unlike host
    /// relations this is available to every IOC type, including IP groups.
    /// </summary>
    private async Task<PagedResult<IocGroupResult>> GetMetadataRelatedAsync(
        IoC seed,
        IocRelationKind relation,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var seedRows = dbContext.Iocs.AsNoTracking()
            .Where(i => i.IocType == seed.IocType
                && i.IocValueHash == seed.IocValueHash
                && i.IocValue == seed.IocValue);

        var keys = relation == IocRelationKind.Malware
            ? await seedRows.Select(i => EF.Property<string>(i, "MalwareKey")).ToListAsync(cancellationToken)
            : await seedRows.Select(i => EF.Property<string>(i, "TargetKey")).ToListAsync(cancellationToken);
        var values = keys
            .Where(key => !string.IsNullOrEmpty(key))
            .Distinct()
            .ToList();

        if (values.Count == 0)
        {
            return PagedResult<IocGroupResult>.Empty(page, pageSize);
        }

        var relatedRows = relation == IocRelationKind.Malware
            ? dbContext.Iocs.AsNoTracking().Where(i => values.Contains(EF.Property<string>(i, "MalwareKey")))
            : dbContext.Iocs.AsNoTracking().Where(i => values.Contains(EF.Property<string>(i, "TargetKey")));

        // Exclude the resolved group itself; a group is identified by its normalized identity.
        relatedRows = relatedRows.Where(i => !(i.IocType == seed.IocType && i.IocValue == seed.IocValue));
        return await PageGroupsAsync(relatedRows, page, pageSize, cancellationToken);
    }

    private async Task<PagedResult<IocGroupResult>> PageGroupsAsync(
        IQueryable<IoC> relatedRows,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var grouped = relatedRows.GroupBy(i => new { i.IocType, i.IocValue });

        var totalCount = await grouped.CountAsync(cancellationToken);
        if (totalCount == 0)
        {
            return PagedResult<IocGroupResult>.Empty(page, pageSize);
        }

        var ordered = grouped
            .OrderBy(g => g.Max(i => i.LastSeenUtc) == null ? 1 : 0)
            .ThenByDescending(g => g.Max(i => i.LastSeenUtc))
            .ThenBy(g => g.Key.IocType)
            .ThenBy(g => g.Key.IocValue);
        var pageKeyRows = await ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(g => new { g.Key.IocType, g.Key.IocValue })
            .ToListAsync(cancellationToken);

        var pageKeys = pageKeyRows.Select(row => new GroupKey(row.IocType, row.IocValue)).ToList();
        var items = await HydrateKeysAsync(pageKeys, cancellationToken);
        return new PagedResult<IocGroupResult>(items, page, pageSize, totalCount);
    }

    private async Task<int[]?> ResolveProviderIdsAsync(
        string provider,
        CancellationToken cancellationToken)
    {
        var providerTokens = provider
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (providerTokens.Length > 1)
        {
            var resolved = new HashSet<int>();
            foreach (var token in providerTokens)
            {
                var resolvedIds = await ResolveProviderIdsAsync(token, cancellationToken);
                if (resolvedIds is not null) resolved.UnionWith(resolvedIds);
            }

            return resolved.Count == 0 ? null : resolved.ToArray();
        }

        if (int.TryParse(provider, out var providerId) && providerId > 0)
        {
            return [providerId];
        }

        var lowered = provider.ToLowerInvariant();
        var ids = await dbContext.Providers.AsNoTracking()
            .Where(p => p.Name.ToLower() == lowered)
            .Select(p => p.Id)
            .ToArrayAsync(cancellationToken);

        return ids.Length == 0 ? null : ids;
    }

    private async Task<IReadOnlyList<IocGroupResult>> HydrateKeysAsync(
        IReadOnlyList<GroupKey> keys,
        CancellationToken cancellationToken)
    {
        if (keys.Count == 0)
        {
            return [];
        }

        var values = keys.Select(key => key.IocValue).Distinct().ToList();
        var rows = await dbContext.Iocs.AsNoTracking()
            .Where(i => values.Contains(i.IocValue))
            .ToListAsync(cancellationToken);

        var wanted = keys.ToHashSet();
        var matching = rows.Where(row => wanted.Contains(new GroupKey(row.IocType, row.IocValue))).ToList();
        var groups = await BuildGroupsAsync(matching, cancellationToken);

        return keys
            .Where(key => groups.ContainsKey((key.IocType, key.IocValue)))
            .Select(key => groups[(key.IocType, key.IocValue)])
            .ToList();
    }

    private async Task<Dictionary<(IocType Type, string Value), IocGroupResult>> BuildGroupsAsync(
        IReadOnlyList<IoC> rows,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<(IocType Type, string Value), IocGroupResult>();
        if (rows.Count == 0)
        {
            return result;
        }

        var providerIds = rows.Select(row => row.ProviderId).Distinct().ToList();
        var providers = await dbContext.Providers.AsNoTracking()
            .Where(provider => providerIds.Contains(provider.Id))
            .ToDictionaryAsync(provider => provider.Id, provider => provider.Name, cancellationToken);

        var rowIds = rows.Select(row => row.Id).ToList();
        var tags = await (
            from iocTag in dbContext.IoCTags.AsNoTracking()
            join tag in dbContext.Tags.AsNoTracking() on iocTag.TagId equals tag.Id
            where rowIds.Contains(iocTag.IocId)
            select new RowTag(iocTag.IocId, tag.Name)).ToListAsync(cancellationToken);

        foreach (var group in rows.GroupBy(row => (Type: row.IocType, Value: row.IocValue)))
        {
            var members = group
                .OrderBy(row => row.ProviderId)
                .ThenBy(row => row.Port ?? 0)
                .ThenBy(row => row.Id)
                .ToList();

            var sources = members
                .Select(row => MapSource(row, providers, tags))
                .ToArray();

            var firstSeen = members.Min(row => row.FirstSeenUtc);
            DateTime? lastSeen = members.Any(row => row.LastSeenUtc.HasValue)
                ? members.Max(row => row.LastSeenUtc)
                : null;

            result[group.Key] = new IocGroupResult(
                members.Min(row => row.Id),
                group.Key.Value,
                IocTypeToken.ToToken(group.Key.Type),
                members.Select(row => row.UrlHost).FirstOrDefault(host => !string.IsNullOrEmpty(host)),
                members.Select(row => row.ParentDomain).FirstOrDefault(host => !string.IsNullOrEmpty(host)),
                firstSeen,
                lastSeen,
                sources);
        }

        return result;
    }

    private IocSourceResult MapSource(
        IoC row,
        IReadOnlyDictionary<int, string> providers,
        IReadOnlyList<RowTag> tags)
    {
        var rowTags = tags
            .Where(tag => tag.IocId == row.Id)
            .Select(tag => tag.Name)
            .ToArray();

        return new IocSourceResult(
            providers.TryGetValue(row.ProviderId, out var provider) ? provider : row.ProviderId.ToString(),
            row.ProviderRecordId,
            row.ProviderDetailsLink,
            row.Port,
            row.FirstSeenUtc,
            row.LastSeenUtc,
            row.ThreatType,
            row.Malware,
            row.MalwareAlias,
            row.MalwarePrintable,
            row.IsCompromised,
            row.Reporter,
            row.Anonymous,
            row.IsVerified,
            row.Target,
            ParseDetails(row.PhishTankDetails),
            row.PhishTankOnline,
            row.ThreatFoxConfidenceLevel,
            rowTags,
            falsePositiveEvaluator.IsFalsePositive(row.UpdatedAt));
    }

    private static JsonElement? ParseDetails(string? details)
    {
        if (string.IsNullOrWhiteSpace(details))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(details);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool IsIp(IocType type) => type is IocType.Ipv4 or IocType.Ipv6;

    private sealed record GroupKey(IocType IocType, string IocValue);

    private sealed record RowTag(int IocId, string Name);
}
