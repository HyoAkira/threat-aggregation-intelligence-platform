using ThreatIntelligence.Domain.Entities;

namespace ThreatIntelligence.Infrastructure.Services;

public sealed record ProviderIoc(Ioc Ioc, IReadOnlyCollection<string> Tags);
