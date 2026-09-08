using Microsoft.EntityFrameworkCore;
using ThreatIntelligence.Domain.Entities;
using ThreatIntelligence.Infrastructure;

namespace ThreatIntelligence.Application.Tests.Persistence;

public class CommonModelConfigurationTests
{
    [Fact]
    public void DbContextExposesOnlyCommonIngestionEntitySets()
    {
        using var context = new ThreatIntelDbContext(
            new DbContextOptionsBuilder<ThreatIntelDbContext>()
                .UseSqlServer("Server=localhost;Database=ThreatIntelModelTests;Trusted_Connection=True")
                .Options);

        var entityTypes = context.Model.GetEntityTypes().Select(type => type.ClrType).ToHashSet();

        Assert.Contains(typeof(Provider), entityTypes);
        Assert.Contains(typeof(Ioc), entityTypes);
        Assert.Contains(typeof(Tag), entityTypes);
        Assert.Contains(typeof(IocTag), entityTypes);
        Assert.DoesNotContain(entityTypes, type => type.Name is "ThreatFox" or "PhishTank");
    }

    [Fact]
    public void IocConfigurationUsesProviderScopedIdentity()
    {
        using var context = new ThreatIntelDbContext(
            new DbContextOptionsBuilder<ThreatIntelDbContext>()
                .UseSqlServer("Server=localhost;Database=ThreatIntelModelTests;Trusted_Connection=True")
                .Options);

        var ioc = context.Model.FindEntityType(typeof(Ioc))!;

        Assert.Contains(ioc.GetIndexes(), index =>
            index.IsUnique && index.Properties.Select(property => property.Name)
                .SequenceEqual(new[] { nameof(Ioc.ProviderId), nameof(Ioc.IocType), nameof(Ioc.IocValueHash), nameof(Ioc.Port) }));
        Assert.Contains(ioc.GetIndexes(), index =>
            index.IsUnique && index.Properties.Select(property => property.Name)
                .SequenceEqual(new[] { nameof(Ioc.ProviderId), nameof(Ioc.ProviderRecordId) }));
    }
}
