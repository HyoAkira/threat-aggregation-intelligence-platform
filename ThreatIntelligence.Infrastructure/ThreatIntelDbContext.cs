using Microsoft.EntityFrameworkCore;
using ThreatIntelligence.Domain;

namespace ThreatIntelligence.Infrastructure;

public class ThreatIntelDbContext : DbContext
{
    public ThreatIntelDbContext(DbContextOptions<ThreatIntelDbContext> options)
        : base(options) { }

    public DbSet<Provider> Providers => Set<Provider>();
    public DbSet<ThreatFox> ThreatFox => Set<ThreatFox>();
    public DbSet<PhishTank> PhishTank => Set<PhishTank>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ThreatIntelDbContext).Assembly);
    }
}
