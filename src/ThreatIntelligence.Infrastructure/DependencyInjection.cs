using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ThreatIntelligence.Infrastructure.Configuration;
using ThreatIntelligence.Infrastructure.HealthChecks;

namespace ThreatIntelligence.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = ThreatIntelligenceOptions.BindAndValidate(configuration);

        services.AddSingleton(options);
        services.AddSingleton(options.Database);
        services.AddSingleton(options.RabbitMq);

        services.AddHealthChecks()
            .AddCheck<SqlServerHealthCheck>("sqlserver", tags: ["ready"])
            .AddCheck<RabbitMqHealthCheck>("rabbitmq", tags: ["ready"]);

        return services;
    }
}
