using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using ThreatIntelligence.Domain.Entities;
using ThreatIntelligence.Domain.Enums;
using ThreatIntelligence.Infrastructure.Persistence;
using ThreatIntelligence.Infrastructure.Persistence.Seeding;

namespace ThreatIntelligence.IntegrationTests;

[Collection("sqlserver")]
public class MigrationAndSeedTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task Migration_applied_once_on_an_empty_database()
    {
        await using var context = fixture.CreateContext();
        var tables = await context.Database.SqlQueryRaw<string>("SELECT name FROM sys.tables WHERE is_ms_shipped = 0")
            .ToListAsync();
        Assert.Contains("providers", tables);
        Assert.Contains("iocs", tables);
        Assert.Contains("iocTags", tables);
        Assert.Contains("fetchHistory", tables);
        Assert.Contains("users", tables);
        Assert.Contains("settings", tables);
        Assert.Contains("tags", tables);
        Assert.Contains("encryptedSecrets", tables);
        Assert.Contains("aiConnectionSettings", tables);
        Assert.Contains("aiReportPolicyVersions", tables);
        Assert.Contains("aiReportPolicyActive", tables);

        var applied = await context.Database
            .SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM __EFMigrationsHistory")
            .ToListAsync();
        Assert.Single(applied);
        Assert.Equal(context.Database.GetMigrations().Count(), applied[0]);
    }

    [Fact]
    public async Task Reapplying_migrations_is_idempotent()
    {
        await using var context = fixture.CreateContext();
        var countBefore = await context.Providers.CountAsync();
        var iocCountBefore = await context.Iocs.CountAsync();

        await context.Database.MigrateAsync();

        var countAfter = await context.Providers.CountAsync();
        Assert.Equal(countBefore, countAfter);
        Assert.Equal(iocCountBefore, await context.Iocs.CountAsync());
        Assert.Equal(
            context.Database.GetMigrations().Count(),
            await context.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM __EFMigrationsHistory").SingleAsync());
    }

    [Fact]
    public async Task Migration_does_not_alter_existing_ioc_or_provider_rows()
    {
        await using var context = fixture.CreateContext();
        var providersBefore = await context.Providers.OrderBy(p => p.Id).ToListAsync();
        var providerSnapshot = providersBefore
            .Select(p => (p.Id, p.Name, p.BaseUrl, p.IsEnabled, p.IntervalMinutes))
            .ToList();
        var iocRowsBefore = await context.Iocs.CountAsync();

        await context.Database.MigrateAsync();

        context.ChangeTracker.Clear();
        var providersAfter = await context.Providers.OrderBy(p => p.Id).ToListAsync();
        var providerSnapshotAfter = providersAfter
            .Select(p => (p.Id, p.Name, p.BaseUrl, p.IsEnabled, p.IntervalMinutes))
            .ToList();
        Assert.Equal(providerSnapshot, providerSnapshotAfter);
        Assert.Equal(iocRowsBefore, await context.Iocs.CountAsync());

        var aiSettingsRows = await context.AiConnectionSettings.CountAsync();
        var policyVersions = await context.AiReportPolicyVersions.CountAsync();
        var activePointer = await context.AiReportPolicyActive.CountAsync();
        Assert.Equal(0, aiSettingsRows);
        Assert.Equal(0, policyVersions);
        Assert.Equal(0, activePointer);
    }

    [Fact]
    public async Task ThreatFox_row_matches_the_documented_defaults()
    {
        await using var context = fixture.CreateContext();
        var provider = context.Providers.Single(p => p.Id == 1);
        provider.SetEnabled(true);
        provider.SetIntervalMinutes(5);
        await context.SaveChangesAsync();
        await new ProviderSeeder(context).SeedAsync(CancellationToken.None);
        context.ChangeTracker.Clear();
        provider = await context.Providers.SingleAsync(p => p.Id == 1);

        Assert.Equal("ThreatFox", provider.Name);
        Assert.Equal("https://threatfox-api.abuse.ch", provider.BaseUrl);
        Assert.True(provider.IsEnabled);
        Assert.Equal(5, provider.IntervalMinutes);
        Assert.Null(provider.NextFetchTime);
        Assert.Equal(FetchMode.Recent, provider.DefaultMode);
        Assert.True(provider.HasMode(FetchMode.Recent));
        Assert.True(provider.HasMode(FetchMode.Full));
        Assert.Equal(5, provider.RecentCooldownMinutes);
        Assert.Equal(60, provider.FullCooldownMinutes);
        Assert.Equal(5, provider.MinimumIntervalMinutes);
    }

    [Fact]
    public async Task PhishTank_row_matches_the_documented_defaults()
    {
        await using var context = fixture.CreateContext();
        var provider = await context.Providers.SingleAsync(p => p.Id == 2);

        Assert.Equal("PhishTank", provider.Name);
        Assert.Equal("https://data.phishtank.com", provider.BaseUrl);
        Assert.True(provider.IsEnabled);
        Assert.Equal(480, provider.IntervalMinutes);
        Assert.Null(provider.NextFetchTime);
        Assert.Equal(FetchMode.Full, provider.DefaultMode);
        Assert.True(provider.HasMode(FetchMode.Full));
        Assert.False(provider.HasMode(FetchMode.Recent));
        Assert.Null(provider.RecentCooldownMinutes);
        Assert.Null(provider.FullCooldownMinutes);
        Assert.Equal(480, provider.MinimumIntervalMinutes);
    }

    [Fact]
    public async Task Reseeding_preserves_operator_changes_without_adding_rows()
    {
        await using var context = fixture.CreateContext();
        var operatorEdited = context.Providers.Single(p => p.Id == 1);
        operatorEdited.SetEnabled(false);
        operatorEdited.SetIntervalMinutes(90);
        await context.SaveChangesAsync();

        await new ProviderSeeder(context).SeedAsync(CancellationToken.None);

        var after = context.Providers.Single(p => p.Id == 1);
        Assert.False(after.IsEnabled);
        Assert.Equal(90, after.IntervalMinutes);
        Assert.Equal(2, await context.Providers.CountAsync());
    }
}