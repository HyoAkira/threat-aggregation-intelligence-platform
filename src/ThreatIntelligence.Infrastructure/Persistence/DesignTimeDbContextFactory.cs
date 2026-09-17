using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using ThreatIntelligence.Infrastructure.Configuration;

namespace ThreatIntelligence.Infrastructure.Persistence;

/// <summary>
/// Used only by the EF tooling at design time. The runtime connection string comes from configuration.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ThreatIntelligenceDbContext>
{
    public ThreatIntelligenceDbContext CreateDbContext(string[] args)
    {
        EnvironmentConfiguration.LoadDotEnv();

        var connectionString = Environment.GetEnvironmentVariable("DATABASE__CONNECTIONSTRING")
            ?? throw new InvalidOperationException(
                "DATABASE__CONNECTIONSTRING is not configured. Set it in .env or the environment.");

        var options = new DbContextOptionsBuilder<ThreatIntelligenceDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new ThreatIntelligenceDbContext(options);
    }
}
