using System.Runtime.CompilerServices;
using System.Text.Json;
using ThreatIntelligence.Application.ProviderRecords;

namespace ThreatIntelligence.Infrastructure.ThreatFox;

/// <summary>
/// Streams a ThreatFox JSON export (a top-level JSON array) and yields one canonical provider
/// record per accepted record. A malformed or unmappable record raises a
/// <see cref="ThreatFoxRecordParseException"/> naming the record index; it is never silently dropped.
/// </summary>
public sealed class ThreatFoxRecentParser(ThreatFoxRecordMapper mapper)
{
    public async IAsyncEnumerable<ProviderRecordInput> ParseAsync(
        Stream stream,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var recordIndex = 0;

        await foreach (var record in JsonSerializer.DeserializeAsyncEnumerable<ThreatFoxJsonRecord>(
                           stream,
                           new JsonSerializerOptions(JsonSerializerDefaults.Web),
                           cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            recordIndex++;
            if (record is null)
            {
                throw new ThreatFoxRecordParseException(recordIndex, "record", "Empty record in feed.");
            }

            yield return mapper.Map(record, recordIndex);
        }
    }
}