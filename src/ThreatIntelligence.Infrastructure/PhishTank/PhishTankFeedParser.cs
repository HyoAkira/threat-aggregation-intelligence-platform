using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Text.Json;
using ThreatIntelligence.Application.ProviderRecords;

namespace ThreatIntelligence.Infrastructure.PhishTank;

public sealed class PhishTankFeedParser(PhishTankRecordMapper mapper)
{
    public async IAsyncEnumerable<ProviderRecordInput> ParseAsync(
        Stream gzipStream,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var decompressed = OpenDecompressedStream(gzipStream);

        var recordIndex = 0;
        await foreach (var record in DeserializeAsync(decompressed, cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            recordIndex++;
            if (record is null) throw new PhishTankRecordParseException(recordIndex, "record", "Empty record in feed.");
            yield return mapper.Map(record, recordIndex);
        }
    }

    private static Stream OpenDecompressedStream(Stream gzipStream)
    {
        try
        {
            return new GZipStream(gzipStream, CompressionMode.Decompress, leaveOpen: true);
        }
        catch (InvalidDataException ex)
        {
            throw new PhishTankDecompressionException("PhishTank feed is not valid gzip; it may be corrupt or truncated.", ex);
        }
    }

    private static async IAsyncEnumerable<PhishTankFeedEntry?> DeserializeAsync(
        Stream stream,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        IAsyncEnumerable<PhishTankFeedEntry?> enumerable;
        try
        {
            enumerable = JsonSerializer.DeserializeAsyncEnumerable<PhishTankFeedEntry>(
                stream,
                new JsonSerializerOptions(JsonSerializerDefaults.Web),
                cancellationToken);
        }
        catch (InvalidDataException ex)
        {
            throw new PhishTankDecompressionException("PhishTank feed is not valid gzip; it may be corrupt or truncated.", ex);
        }
        catch (JsonException ex)
        {
            throw new PhishTankDecompressionException("PhishTank feed contains invalid JSON; it may be corrupt or truncated.", ex);
        }

        await foreach (var record in enumerable.ConfigureAwait(false))
        {
            yield return record;
        }
    }
}
