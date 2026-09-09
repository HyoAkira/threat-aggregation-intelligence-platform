using MediatR;
using Microsoft.Extensions.DependencyInjection;
using ThreatIntelligence.Application.Features.Iocs.Queries.SearchIocs;

namespace ThreatIntelligence.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddMediatR(configuration =>
            configuration.RegisterServicesFromAssemblyContaining<SearchIocsQuery>());

        return services;
    }
}
