using ThreatIntelligence.Application.ProviderRecords;

namespace ThreatIntelligence.Application.BatchWriter;

/// <summary>
/// Result of applying one validated provider batch to the IOC store.
/// </summary>
public sealed record IocBatchResult(int Added, int Updated);

/// <summary>
/// The shared atomic upsert/deduplication pipeline. Consumes validated E03
/// <see cref="NormalizedProviderRecord"/> instances and commits them, chunked, in a single
/// outer transaction. Identity conflicts and invalid writes abort the whole batch.
/// </summary>
public interface IIocBatchWriter
{
    Task<IocBatchResult> ApplyAsync(
        IReadOnlyList<NormalizedProviderRecord> records,
        int providerId,
        CancellationToken cancellationToken);
}