using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ThreatIntelligence.Application.ProviderRecords;

namespace ThreatIntelligence.Infrastructure.PhishTank;

public sealed class PhishTankFeedParser(
    PhishTankRecordMapper mapper,
    ILogger<PhishTankFeedParser>? logger = null)
{
    private readonly ILogger<PhishTankFeedParser> _logger =
        logger ?? NullLogger<PhishTankFeedParser>.Instance;

    public async IAsyncEnumerable<ProviderRecordInput> ParseAsync(
        Stream gzipStream,
        [EnumeratorCancellation] CancellationToken cancellationToken,
        bool skipMalformedRecords = false)
    {
        await using var decompressed = OpenDecompressedStream(gzipStream);

        var recordIndex = 0;
        await foreach (var record in DeserializeAsync(decompressed, cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            recordIndex++;

            ProviderRecordInput mapped;
            try
            {
                if (record is null) throw new PhishTankRecordParseException(recordIndex, "record", "Empty record in feed.");
                mapped = mapper.Map(record, recordIndex);
            }
            catch (PhishTankRecordParseException ex) when (skipMalformedRecords)
            {
                _logger.LogWarning(
                    ex,
                    "PhishTank record skipped RecordIndex={RecordIndex} Field={Field} Reason={Reason}",
                    ex.RecordIndex,
                    ex.Field,
                    ex.Reason);
                continue;
            }

            yield return mapped;
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
