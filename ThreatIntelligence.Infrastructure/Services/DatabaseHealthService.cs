using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ThreatIntelligence.Infrastructure.Services;

public class DatabaseHealthService
{
    private readonly ThreatIntelDbContext _dbContext;

    public DatabaseHealthService(ThreatIntelDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> CanConnectAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Database.CanConnectAsync(cancellationToken);
    }

    public string GetConnectionTarget()
    {
        try
        {
            var connectionString = _dbContext.Database.GetConnectionString();
            if (string.IsNullOrWhiteSpace(connectionString))
                return "(no connection string configured)";

            var builder = new SqlConnectionStringBuilder(connectionString);
            return builder.DataSource;
        }
        catch
        {
            return "(unavailable)";
        }
    }
}
