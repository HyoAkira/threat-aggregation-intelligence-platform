using Microsoft.EntityFrameworkCore;

namespace ThreatIntelligence.Infrastructure.Persistence;

/// <summary>
/// Applies pending migrations and seeds the deterministic provider rows at startup. Shared by
/// both API and Worker hosts; idempotent so overlapping startup is safe.
/// </summary>
public sealed class DatabaseInitializer(
    ThreatIntelligenceDbContext dbContext,
    Seeding.ProviderSeeder providerSeeder,
    Seeding.AdminSeeder adminSeeder)
{
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await dbContext.Database.MigrateAsync(cancellationToken);
        await providerSeeder.SeedAsync(cancellationToken);
        await adminSeeder.SeedAsync(cancellationToken);
    }
}
