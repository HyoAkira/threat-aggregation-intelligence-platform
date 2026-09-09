using System.Data;
using Microsoft.EntityFrameworkCore;
using ThreatIntelligence.Infrastructure.Persistence;

namespace ThreatIntelligence.Infrastructure.Persistence.Seeding;

/// <summary>
/// Seeds the two deterministic provider rows (ThreatFox id 1, PhishTank id 2) insert-if-absent.
/// Explicit IDs require IDENTITY_INSERT; existence is checked by id so operator changes to
/// enabled/interval are never overwritten.
/// </summary>
public sealed class ProviderSeeder(ThreatIntelligenceDbContext dbContext)
{
    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        await SeedProviderAsync(
            1,
            "ThreatFox",
            "https://threatfox-api.abuse.ch",
            intervalMinutes: 5,
            cancellationToken);

        await SeedProviderAsync(
            2,
            "PhishTank",
            "https://data.phishtank.com",
            intervalMinutes: 480,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    private async Task SeedProviderAsync(
        int id,
        string name,
        string baseUrl,
        int intervalMinutes,
        CancellationToken cancellationToken)
    {
        await dbContext.Database.ExecuteSqlAsync($"""
            IF NOT EXISTS (SELECT 1 FROM providers WITH (UPDLOCK, HOLDLOCK) WHERE id = {id})
            BEGIN
                SET IDENTITY_INSERT providers ON;
                INSERT INTO providers (id, name, baseUrl, isEnabled, intervalMinutes, lastFetchStatus, lastFetchAt, nextFetchTime)
                VALUES ({id}, {name}, {baseUrl}, 1, {intervalMinutes}, NULL, NULL, NULL);
                SET IDENTITY_INSERT providers OFF;
            END
            """, cancellationToken);
    }
}
