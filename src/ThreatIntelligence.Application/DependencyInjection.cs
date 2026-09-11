using MediatR;
using Microsoft.Extensions.DependencyInjection;
using ThreatIntelligence.Application.Features.Iocs.Queries.SearchIocs;
using ThreatIntelligence.Application.Normalization;
using ThreatIntelligence.Application.ProviderRecords;

namespace ThreatIntelligence.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddMediatR(configuration =>
            configuration.RegisterServicesFromAssemblyContaining<SearchIocsQuery>());
        services.AddSingleton<IIpv4Normalizer, Ipv4Normalizer>();
        services.AddSingleton<IIpv6Normalizer, Ipv6Normalizer>();
        services.AddSingleton<IUrlNormalizer, UrlNormalizer>();
        services.AddSingleton<IDomainNormalizer, DomainNormalizer>();
        services.AddSingleton<IIocClassifier, IocClassifier>();
        services.AddSingleton<ProviderRecordValidator>();

        return services;
    }
}
