using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ThreatIntelligence.Application.Abstractions.Authentication;
using ThreatIntelligence.Application.Abstractions.Hashing;
using ThreatIntelligence.Application.Abstractions.Persistence;
using ThreatIntelligence.Application.Fetches;
using ThreatIntelligence.Application.Normalization;
using ThreatIntelligence.Infrastructure.Configuration;
using ThreatIntelligence.Infrastructure.Fetches;
using ThreatIntelligence.Infrastructure.Authentication;
using ThreatIntelligence.Infrastructure.Hashing;
using ThreatIntelligence.Infrastructure.HealthChecks;
using ThreatIntelligence.Infrastructure.Persistence;
using ThreatIntelligence.Infrastructure.Persistence.Repositories;
using ThreatIntelligence.Infrastructure.Persistence.Seeding;
using ThreatIntelligence.Infrastructure.PublicSuffix;

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
        services.AddSingleton(options.Jwt);
        services.AddSingleton(options.PasswordHash);
        services.AddSingleton(options.ThreatFox);
        services.AddSingleton(options.AdminSeed);
        services.AddSingleton(options.Logging);

        services.AddDbContext<ThreatIntelligenceDbContext>(
            db => db.UseSqlServer(options.Database.ConnectionString),
            contextLifetime: ServiceLifetime.Scoped,
            optionsLifetime: ServiceLifetime.Singleton);
        services.AddDbContextFactory<ThreatIntelligenceDbContext>(db =>
            db.UseSqlServer(options.Database.ConnectionString));

        services.AddScoped<DatabaseInitializer>();
        services.AddScoped<ProviderSeeder>();
        services.AddScoped<AdminSeeder>();

        services.AddScoped<IIocRepository, IocRepository>();
        services.AddScoped<IIocSearchReader, IocSearchReader>();
        services.AddScoped<IUserAuthenticationReader, UserAuthenticationReader>();
        services.AddScoped<IFetchHistoryRepository, FetchHistoryRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IFetchGuard, FetchGuard>();
        services.AddScoped<IFetchLifecycle, FetchLifecycle>();
        services.AddScoped<IFetchJobPublisher, MassTransitFetchJobPublisher>();
        services.AddSingleton<FetchSchedulerService>();
        services.AddScoped<IProviderFetchExecutor, UnavailableProviderFetchExecutor>();
        services.AddHostedService<RuntimeLogLevelWatcher>();

        services.AddSingleton<IValueHasher, Sha256ValueHasher>();
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        services.AddSingleton<ITokenIssuer, JwtTokenIssuer>();
        services.AddSingleton<IParentDomainResolver, PublicSuffixParentDomainResolver>();
        services.AddSingleton(TimeProvider.System);

        services.AddHealthChecks()
            .AddCheck<SqlServerHealthCheck>("sqlserver", tags: ["ready"])
            .AddCheck<RabbitMqHealthCheck>("rabbitmq", tags: ["ready"]);

        return services;
    }
}
