using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ThreatIntelligence.Application.Abstractions;

namespace ThreatIntelligence.Infrastructure.Services;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<ThreatIntelDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<DatabaseHealthService>();

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
