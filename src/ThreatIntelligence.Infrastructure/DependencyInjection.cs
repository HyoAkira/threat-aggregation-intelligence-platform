using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ThreatIntelligence.Application.Abstractions.Hashing;
using ThreatIntelligence.Application.Abstractions.Persistence;
using ThreatIntelligence.Infrastructure.Configuration;
using ThreatIntelligence.Infrastructure.Hashing;
using ThreatIntelligence.Infrastructure.HealthChecks;
using ThreatIntelligence.Infrastructure.Persistence;
using ThreatIntelligence.Infrastructure.Persistence.Repositories;
using ThreatIntelligence.Infrastructure.Persistence.Seeding;

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

        services.AddDbContext<ThreatIntelligenceDbContext>(db =>
            db.UseSqlServer(options.Database.ConnectionString));
        services.AddDbContextFactory<ThreatIntelligenceDbContext>(db =>
            db.UseSqlServer(options.Database.ConnectionString));

        services.AddScoped<DatabaseInitializer>();
        services.AddScoped<ProviderSeeder>();
        services.AddScoped<AdminSeeder>();

        services.AddScoped<IIocRepository, IocRepository>();
        services.AddScoped<IFetchHistoryRepository, FetchHistoryRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddSingleton<IValueHasher, Sha256ValueHasher>();
        services.AddSingleton(TimeProvider.System);

        services.AddHealthChecks()
            .AddCheck<SqlServerHealthCheck>("sqlserver", tags: ["ready"])
            .AddCheck<RabbitMqHealthCheck>("rabbitmq", tags: ["ready"]);

        return services;
    }
}
