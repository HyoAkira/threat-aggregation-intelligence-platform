using ThreatIntelligence.Application.ProviderRecords;
using ThreatIntelligence.Domain.Entities;

namespace ThreatIntelligence.Application.BatchWriter;

/// <summary>
/// Provider-specific mapping from a validated <see cref="NormalizedProviderRecord"/> to the flat
/// <see cref="IocUpsertFields"/> the shared atomic batch writer persists.
/// </summary>
public interface IIocUpsertFieldsMapper
{
    IocUpsertFields Map(NormalizedProviderRecord record);
}
