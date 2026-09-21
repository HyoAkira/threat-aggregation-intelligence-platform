using DotNetEnv;

namespace ThreatIntelligence.IntegrationTests;

/// <summary>
/// Reads the same RabbitMQ settings the hosts use from the repo <c>.env</c> file. Defaults match
/// the development broker so the broker tests can run against the Compose service.
/// </summary>
public sealed class RabbitMqFixture : IAsyncLifetime
{
    public string Host { get; private set; } = "localhost";

    public int Port { get; private set; } = 5672;

    public string User { get; private set; } = "guest";

    public string Password { get; private set; } = "guest";

    public int ManagementPort { get; private set; } = 15672;

    public Uri AmqpUri => new($"rabbitmq://{Host}:{Port}/");

    public Task InitializeAsync()
    {
        Env.Load(FindEnvPath());
        Host = Environment.GetEnvironmentVariable("RABBITMQ__HOST") ?? "localhost";
        Port = int.TryParse(Environment.GetEnvironmentVariable("RABBITMQ__PORT"), out var port) ? port : 5672;
        User = Environment.GetEnvironmentVariable("RABBITMQ__USER") ?? "guest";
        Password = Environment.GetEnvironmentVariable("RABBITMQ__PASSWORD") ?? "guest";
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

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

[CollectionDefinition("rabbitmq")]
public sealed class RabbitMqCollection : ICollectionFixture<RabbitMqFixture>
{
}
