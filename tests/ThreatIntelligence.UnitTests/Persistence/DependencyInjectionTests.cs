using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ThreatIntelligence.Infrastructure;
using ThreatIntelligence.Infrastructure.Persistence;

namespace ThreatIntelligence.UnitTests.Persistence;

public class DependencyInjectionTests
{
    private static ServiceProvider BuildProvider()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:ConnectionString"] = "Server=localhost;Database=test;User Id=sa;Password=not-real;TrustServerCertificate=True",
                ["RabbitMq:Host"] = "localhost",
                ["RabbitMq:Port"] = "5672",
                ["RabbitMq:User"] = "guest",
                ["RabbitMq:Password"] = "guest",
                ["RabbitMq:Exchange"] = "threat-intel",
                ["Jwt:Secret"] = new string('s', 40),
                ["Jwt:Issuer"] = "issuer",
                ["Jwt:Audience"] = "audience",
                ["Jwt:ExpiryMinutes"] = "480",
                ["ThreatFox:ApiKey"] = "key",
                ["Admin:Username"] = "admin",
                ["Admin:Password"] = "password",
                ["Logging:MinimumLevel"] = "Information",
            })
            .Build();

        return new ServiceCollection()
            .AddInfrastructureServices(config)
            .BuildServiceProvider();
    }

    [Fact]
    public void ThreatIntelligenceDbContext_resolves_from_the_container()
    {
        using var provider = BuildProvider();
        using var context = provider.GetRequiredService<ThreatIntelligenceDbContext>();
        Assert.NotNull(context);
    }

    [Fact]
    public void DatabaseInitializer_and_all_seed_dependencies_resolve_from_the_container()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<DatabaseInitializer>());
    }
}
