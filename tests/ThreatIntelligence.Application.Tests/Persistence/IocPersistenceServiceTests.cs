using Microsoft.EntityFrameworkCore;
using ThreatIntelligence.Domain.Entities;
using ThreatIntelligence.Domain.Enums;
using ThreatIntelligence.Infrastructure;
using ThreatIntelligence.Infrastructure.Services;

namespace ThreatIntelligence.Application.Tests.Persistence;

public class IocPersistenceServiceTests
{
    [Fact]
    public async Task UpsertRefreshesProviderIocAndReplacesTags()
    {
        await using var context = await CreateContextAsync();
        try
        {
            var service = new IocPersistenceService(context);

            await service.PersistAsync("ThreatFox", [Record("tf-1", "example.com", ["first", "shared"])]);
            await service.PersistAsync("ThreatFox", [Record("tf-1", "example.com", ["second", "shared"])]);

            var ioc = await context.Iocs.Include(item => item.IocTags).ThenInclude(join => join.Tag).SingleAsync();
            Assert.Equal(["second", "shared"], ioc.IocTags.Select(join => join.Tag.Name).OrderBy(name => name));
            Assert.Equal(3, await context.Tags.CountAsync());
        }
        finally
        {
            await context.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    public async Task EquivalentValuesFromDifferentProvidersRemainSeparate()
    {
        await using var context = await CreateContextAsync();
        try
        {
            var service = new IocPersistenceService(context);

            await service.PersistAsync("ThreatFox", [Record("tf-1", "example.com", [])]);
            await service.PersistAsync("PhishTank", [Record("pt-1", "example.com", [])]);

            Assert.Equal(2, await context.Iocs.CountAsync());
        }
        finally
        {
            await context.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    public async Task SharedTagsRemainWhenAnotherIocReplacesItsTags()
    {
        await using var context = await CreateContextAsync();
        try
        {
            var service = new IocPersistenceService(context);

            await service.PersistAsync("ThreatFox", [Record("tf-1", "first.example", ["shared"])]);
            await service.PersistAsync("ThreatFox", [Record("tf-2", "second.example", ["shared"])]);
            await service.PersistAsync("ThreatFox", [Record("tf-1", "first.example", ["replacement"])]);

            var secondIoc = await context.Iocs
                .Include(item => item.IocTags)
                .ThenInclude(join => join.Tag)
                .SingleAsync(item => item.ProviderRecordId == "tf-2");

            Assert.Equal("shared", Assert.Single(secondIoc.IocTags).Tag.Name);
            Assert.Equal(2, await context.Tags.CountAsync());
        }
        finally
        {
            await context.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    public async Task ConflictingProviderIdentitiesAreRejected()
    {
        await using var context = await CreateContextAsync();
        try
        {
            var service = new IocPersistenceService(context);

            await service.PersistAsync("ThreatFox", [Record("tf-1", "first.example", [])]);
            await service.PersistAsync("ThreatFox", [Record("tf-2", "second.example", [])]);

            await Assert.ThrowsAsync<InvalidDataException>(() =>
                service.PersistAsync("ThreatFox", [Record("tf-1", "second.example", [])]));
        }
        finally
        {
            await context.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    public async Task FailingBatchRollsBackIocsAndTags()
    {
        await using var context = await CreateContextAsync();
        try
        {
            var service = new IocPersistenceService(context);
            await service.PersistAsync("ThreatFox", [Record("tf-1", "first.example", ["existing"])]);

            await Assert.ThrowsAsync<InvalidDataException>(() => service.PersistAsync("ThreatFox",
            [
                Record("tf-2", "second.example", ["rolled-back"]),
                Record("tf-1", "second.example", [])
            ]));

            Assert.Equal(1, await context.Iocs.CountAsync());
            Assert.Equal("first.example", (await context.Iocs.SingleAsync()).IocValue);
            Assert.Equal("existing", (await context.Tags.SingleAsync()).Name);
        }
        finally
        {
            await context.Database.EnsureDeletedAsync();
        }
    }

    private static async Task<ThreatIntelDbContext> CreateContextAsync()
    {
        var password = Environment.GetEnvironmentVariable("SA_PASSWORD")
            ?? throw new InvalidOperationException("SA_PASSWORD must be set for SQL Server persistence tests.");
        var options = new DbContextOptionsBuilder<ThreatIntelDbContext>()
            .UseSqlServer($"Server=localhost,1433;Database=ThreatIntelTests_{Guid.NewGuid():N};User Id=sa;Password={password};TrustServerCertificate=True")
            .Options;
        var context = new ThreatIntelDbContext(options);
        await context.Database.MigrateAsync();
        return context;
    }

    private static ProviderIoc Record(string providerRecordId, string value, IReadOnlyCollection<string> tags)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value));
        return new ProviderIoc(
            new Ioc
            {
                ProviderRecordId = providerRecordId,
                IocValue = value,
                IocValueHash = hash,
                IocType = IocType.Domain,
                FirstSeenUtc = DateTime.UtcNow
            },
            tags);
    }
}
