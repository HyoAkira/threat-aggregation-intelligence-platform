using Microsoft.EntityFrameworkCore;
using ThreatIntelligence.Domain.Entities;
using ThreatIntelligence.Domain.Enums;
using ThreatIntelligence.Infrastructure.Persistence;

namespace ThreatIntelligence.IntegrationTests;

/// <summary>
/// Domain-entity seeding helpers for the query read-model tests. Rows are inserted through the
/// aggregate constructor so the same validation as production writes applies.
/// </summary>
public static class IocQueryTestData
{
    public static async Task<IoC> InsertIocAsync(
        ThreatIntelligenceDbContext context,
        int providerId,
        string recordId,
        string iocValue,
        IocType iocType,
        int? port = null,
        DateTime? firstSeenUtc = null,
        DateTime? lastSeenUtc = null,
        DateTime? updatedAtUtc = null,
        string? urlHost = null,
        string? parentDomain = null,
        byte? confidence = null,
        bool? verified = null,
        string? target = null,
        bool? online = null,
        string? details = null,
        string? threatType = null,
        string? malware = null)
    {
        var firstSeen = firstSeenUtc ?? TestData.Utc;
        var updatedAt = updatedAtUtc ?? firstSeen;
        var row = new IoC(
            providerId: providerId,
            providerRecordId: recordId,
            iocValue: iocValue,
            iocValueHash: TestData.HashOf(iocValue),
            iocType: iocType,
            firstSeenUtc: firstSeen,
            createdAt: updatedAt,
            updatedAt: updatedAt,
            port: port,
            lastSeenUtc: lastSeenUtc,
            threatType: threatType,
            malware: malware,
            isVerified: verified,
            target: target,
            phishTankDetails: details,
            phishTankOnline: online,
            urlHost: urlHost,
            parentDomain: parentDomain,
            threatFoxConfidenceLevel: confidence);

        context.Iocs.Add(row);
        await context.SaveChangesAsync();
        return row;
    }

    public static async Task<Tag> AddTagAsync(
        ThreatIntelligenceDbContext context,
        int iocId,
        string name)
    {
        var normalized = name.Trim().ToLowerInvariant();
        var tag = await context.Tags.SingleOrDefaultAsync(item => item.Name == normalized);
        if (tag is null)
        {
            tag = new Tag(name);
            context.Tags.Add(tag);
            await context.SaveChangesAsync();
        }

        if (!await context.IoCTags.AnyAsync(item => item.IocId == iocId && item.TagId == tag.Id))
        {
            context.IoCTags.Add(new IoCTag(iocId, tag.Id));
        }

        return tag;
    }

    public static async Task TagAsync(ThreatIntelligenceDbContext context, IoC row, string tagName)
    {
        var name = tagName.Trim().ToLowerInvariant();
        var tag = await context.Tags.FirstOrDefaultAsync(item => item.Name == name);
        if (tag is null)
        {
            tag = new Tag(name);
            context.Tags.Add(tag);
            await context.SaveChangesAsync();
        }

        context.IoCTags.Add(new IoCTag(row.Id, tag.Id));
        await context.SaveChangesAsync();
    }
}
