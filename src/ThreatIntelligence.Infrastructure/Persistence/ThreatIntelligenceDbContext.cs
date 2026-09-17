using Microsoft.EntityFrameworkCore;
using ThreatIntelligence.Domain.Entities;

namespace ThreatIntelligence.Infrastructure.Persistence;

/// <summary>
/// Materializes the data-contract schema (§4) in SQL Server. All table and column names use
/// plural lower-camel case; enums persist as their ordinal tinyint values; timestamps are UTC.
/// </summary>
public class ThreatIntelligenceDbContext(DbContextOptions<ThreatIntelligenceDbContext> options)
    : DbContext(options)
{
    public DbSet<Provider> Providers => Set<Provider>();
    public DbSet<IoC> Iocs => Set<IoC>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<IoCTag> IoCTags => Set<IoCTag>();
    public DbSet<FetchHistory> FetchHistory => Set<FetchHistory>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Settings> Settings => Set<Settings>();
    public DbSet<EncryptedSecret> EncryptedSecrets => Set<EncryptedSecret>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Conventions.Add(_ => new CamelCaseColumnConvention());
        base.ConfigureConventions(configurationBuilder);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ThreatIntelligenceDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
