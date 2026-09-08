using Microsoft.EntityFrameworkCore;
using ThreatIntelligence.Domain;
using ThreatIntelligence.Domain.Entities;

namespace ThreatIntelligence.Infrastructure;

public class ThreatIntelDbContext : DbContext
{
    public ThreatIntelDbContext(DbContextOptions<ThreatIntelDbContext> options)
        : base(options) { }

    public DbSet<Provider> Providers => Set<Provider>();
    public DbSet<Ioc> Iocs => Set<Ioc>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<IocTag> IocTags => Set<IocTag>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ThreatIntelDbContext).Assembly);
    }
}
