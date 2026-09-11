using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ThreatIntelligence.Infrastructure.Persistence;

/// <summary>
/// Used only by the EF tooling at design time. The runtime connection string comes from configuration.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ThreatIntelligenceDbContext>
{
    public ThreatIntelligenceDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ThreatIntelligenceDbContext>()
            .UseSqlServer("Server=localhost;Database=ThreatIntelligence;User Id=sa;Password=design-time-only;TrustServerCertificate=True")
            .Options;

        return new ThreatIntelligenceDbContext(options);
    }
}