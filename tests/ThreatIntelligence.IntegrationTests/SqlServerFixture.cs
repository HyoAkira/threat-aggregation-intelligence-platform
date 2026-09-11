using DotNetEnv;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using ThreatIntelligence.Infrastructure.Persistence;
using ThreatIntelligence.Infrastructure.Persistence.Seeding;

namespace ThreatIntelligence.IntegrationTests;

/// <summary>
/// Creates a disposable test database on a local SQL Server and applies the full migration set.
/// The server is taken from TEST_SQLSERVER_HOST/PORT (defaults localhost:1433) and credentials
/// from TEST_SQLSERVER_PASSWORD or SA_PASSWORD in the repo .env file.
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    private const string DatabaseName = "ThreatIntelligenceTests";

    public string ConnectionString { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Env.Load(FindEnvPath());

        var overrideConnection = Environment.GetEnvironmentVariable("TEST_SQLSERVER_CONNECTIONSTRING");
        if (!string.IsNullOrWhiteSpace(overrideConnection))
        {
            ConnectionString = overrideConnection;
        }
        else
        {
            var host = Environment.GetEnvironmentVariable("TEST_SQLSERVER_HOST") ?? "localhost";
            var port = Environment.GetEnvironmentVariable("TEST_SQLSERVER_PORT") ?? "1432";
            var password = Environment.GetEnvironmentVariable("TEST_SQLSERVER_PASSWORD")
                ?? throw new InvalidOperationException(
                    "TEST_SQLSERVER_PASSWORD must be set in .env for integration tests.");

            var masterConnection = $"Server={host},{port};Database=master;User Id=sa;Password={password};TrustServerCertificate=True;Encrypt=False";
            ConnectionString = $"Server={host},{port};Database={DatabaseName};User Id=sa;Password={password};TrustServerCertificate=True;Encrypt=False";

            var masterOptions = new DbContextOptionsBuilder<ThreatIntelligenceDbContext>()
                .UseSqlServer(masterConnection)
                .Options;
            await using var master = new ThreatIntelligenceDbContext(masterOptions);
            await master.Database.ExecuteSqlRawAsync(
                "IF DB_ID(N'" + DatabaseName + "') IS NOT NULL BEGIN ALTER DATABASE [" + DatabaseName +
                "] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [" + DatabaseName + "]; END");
        }

        await using var context = CreateContext();
        await context.Database.MigrateAsync();

        await new ProviderSeeder(context).SeedAsync(CancellationToken.None);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public ThreatIntelligenceDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ThreatIntelligenceDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;
        return new ThreatIntelligenceDbContext(options);
    }

    public IDbContextFactory<ThreatIntelligenceDbContext> CreateContextFactory()
    {
        var options = new DbContextOptionsBuilder<ThreatIntelligenceDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;
        return new PooledDbContextFactory<ThreatIntelligenceDbContext>(options);
    }

    public async Task ResetDataAsync()
    {
        await using var context = CreateContext();
        await context.Database.ExecuteSqlRawAsync(
            "DELETE FROM iocTags; DELETE FROM iocs; DELETE FROM fetchHistory; DELETE FROM tags; DELETE FROM users; DELETE FROM settings;");
        await context.Database.ExecuteSqlRawAsync(
            "UPDATE providers SET nextFetchTime = NULL, lastFetchAt = NULL, lastFetchStatus = NULL, isEnabled = 1, " +
            "intervalMinutes = CASE WHEN id = 1 THEN 5 ELSE 480 END;");
    }

    private static string FindEnvPath()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, ".env");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return ".env";
    }
}

[CollectionDefinition("sqlserver")]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerFixture>
{
}