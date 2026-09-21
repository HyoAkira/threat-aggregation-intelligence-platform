using DotNetEnv;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using ThreatIntelligence.Application.Abstractions.Secrets;
using ThreatIntelligence.Infrastructure.Configuration;
using ThreatIntelligence.Infrastructure.Persistence;
using ThreatIntelligence.Infrastructure.Persistence.Seeding;
using ThreatIntelligence.Infrastructure.Secrets;

namespace ThreatIntelligence.IntegrationTests;

/// <summary>
/// Creates a disposable test database on a local SQL Server and applies the full migration set.
/// The server is taken from TEST_SQLSERVER_HOST/PORT (defaults localhost:1433) and credentials
/// from TEST_SQLSERVER_PASSWORD or SA_PASSWORD in the repo .env file.
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    private const string DatabaseName = "ThreatIntelligenceTests";
    private static readonly byte[] TestRootKey =
    [
        0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08,
        0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F, 0x10,
        0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17, 0x18,
        0x19, 0x1A, 0x1B, 0x1C, 0x1D, 0x1E, 0x1F, 0x20
    ];

    public string ConnectionString { get; private set; } = null!;

    public ISecretStore SecretStore { get; private set; } = null!;

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
        SecretStore = new EncryptedSecretStore(
            CreateContextFactory(),
            new AesGcmSecretProtector(new EncryptionOptions(TestRootKey, 1, null)),
            new EncryptionOptions(TestRootKey, 1, null),
            TimeProvider.System);
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
            "DELETE FROM aiReportPolicyActive; DELETE FROM aiReportPolicyVersions; DELETE FROM aiConnectionSettings; DELETE FROM encryptedSecrets;");
        await context.Database.ExecuteSqlRawAsync(
            "UPDATE providers SET nextFetchTime = NULL, lastFetchAt = NULL, lastFetchStatus = NULL, isEnabled = 1, " +
            "intervalMinutes = CASE WHEN id = 1 THEN 5 ELSE 480 END;");
        await context.Database.ExecuteSqlRawAsync(
            "UPDATE providers SET defaultMode = CASE WHEN id = 1 THEN 0 ELSE 1 END, " +
            "supportedModes = CASE WHEN id = 1 THEN 3 ELSE 2 END, " +
            "recentCooldownMinutes = CASE WHEN id = 1 THEN 5 ELSE NULL END, " +
            "fullCooldownMinutes = CASE WHEN id = 1 THEN 60 ELSE NULL END, " +
            "minimumIntervalMinutes = CASE WHEN id = 1 THEN 5 ELSE 480 END WHERE id IN (1, 2);");
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