using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ThreatIntelligence.Application.Abstractions;
using ThreatIntelligence.Infrastructure.ConnectionStrings;
using ThreatIntelligence.Infrastructure.Services;

namespace ThreatIntelligence.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = DatabaseConnectionStringResolver.Resolve(
            configuration.GetConnectionString("DefaultConnection"),
            Environment.GetEnvironmentVariable("SA_PASSWORD"));

        services.AddDbContext<ThreatIntelDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddScoped<DatabaseHealthService>();
        services.AddSingleton(new IocNormalizer(
            Path.Combine(AppContext.BaseDirectory, "public_suffix_list.dat")));
        services.AddScoped<IocPersistenceService>();

        services.AddHttpClient<IThreatFoxApiService, ThreatFoxApiService>(client =>
        {
            client.BaseAddress = new Uri("https://threatfox-api.abuse.ch");
        });

        services.AddHttpClient<IPhishTankApiService, PhishTankApiService>(client =>
        {
            client.BaseAddress = new Uri("http://data.phishtank.com/data/");
            client.DefaultRequestHeaders.UserAgent.ParseAdd("phishtank/hyoakira");
        });

        return services;
    }
}
