using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ThreatIntelligence.Application.ProviderRecords;

namespace ThreatIntelligence.Infrastructure.ThreatFox;

/// <summary>
/// Streams a ThreatFox JSON export and yields one canonical provider record per accepted record.
/// Two root shapes are supported:
/// <list type="bullet">
/// <item>a top-level JSON array of records (community-style), and</item>
/// <item>a top-level JSON object keyed by ThreatFox id whose values are arrays of records
/// (the file exports: <c>recent.json</c>, <c>domains_recent.json</c>, <c>full.json</c>).</item>
/// </list>
/// A malformed or unmappable record raises a <see cref="ThreatFoxRecordParseException"/> naming the
/// record index; it is never silently dropped. A body that is neither shape raises a
/// <see cref="ThreatFoxFeedFormatException"/> carrying a capped prefix of what the endpoint returned.
/// </summary>
public sealed class ThreatFoxRecentParser
{
    public const int MaxDiagnosticPrefixBytes = 512;
    public const long MaxBufferedObjectBytes = 256L * 1024 * 1024;

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        // The export has been observed emitting a trailing comma before the closing object brace.
        AllowTrailingCommas = true
    };

    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        AllowTrailingCommas = true
    };

    private readonly ThreatFoxRecordMapper _mapper;
    private readonly ILogger<ThreatFoxRecentParser> _logger;

    public ThreatFoxRecentParser(
        ThreatFoxRecordMapper mapper,
        ILogger<ThreatFoxRecentParser>? logger = null)
    {
        _mapper = mapper;
        _logger = logger ?? NullLogger<ThreatFoxRecentParser>.Instance;
    }

    public async IAsyncEnumerable<ProviderRecordInput> ParseAsync(
        Stream stream,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var captured = new PrefixCapturingStream(stream, MaxDiagnosticPrefixBytes);
        await captured.CaptureAsync(cancellationToken).ConfigureAwait(false);

        var payloadPrefix = DescribePrefix(captured.Prefix.Span);
        var firstToken = FirstNonWhitespaceByte(captured.Prefix.Span);

        var records = firstToken switch
        {
            (byte)'[' => ParseArrayAsync(captured, payloadPrefix, cancellationToken),
            (byte)'{' => ParseKeyedObjectAsync(captured, payloadPrefix, cancellationToken),
            _ => throw BuildShapeFailure(firstToken, payloadPrefix)
        };

        var recordIndex = 0;
        await foreach (var record in records.ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            recordIndex++;
            if (record is null)
            {
                throw new ThreatFoxRecordParseException(recordIndex, "record", "Empty record in feed.");
            }

            yield return _mapper.Map(record, recordIndex);
        }
    }

    private async IAsyncEnumerable<ThreatFoxJsonRecord> ParseArrayAsync(
        PrefixCapturingStream stream,
        string payloadPrefix,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var recordIndex = 0;
        await using var enumerator = JsonSerializer
            .DeserializeAsyncEnumerable<ThreatFoxJsonRecord>(stream, SerializerOptions, cancellationToken)
            .GetAsyncEnumerator(cancellationToken);

        while (true)
        {
            ThreatFoxJsonRecord? record;
            try
            {
                if (!await enumerator.MoveNextAsync().ConfigureAwait(false))
                {
                    break;
                }

                record = enumerator.Current;
            }
            catch (JsonException ex)
            {
                _logger.LogError(
                    ex,
                    "ThreatFox feed JSON parse failure at record {RecordIndex}. PayloadPrefix={PayloadPrefix}",
                    recordIndex + 1,
                    payloadPrefix);
                throw new ThreatFoxFeedFormatException(
                    $"ThreatFox feed JSON parse failure at record {recordIndex + 1}: {ex.Message}. Payload prefix: {payloadPrefix}",
                    payloadPrefix,
                    ex);
            }

            recordIndex++;
            yield return record!;
        }
    }

    private async IAsyncEnumerable<ThreatFoxJsonRecord> ParseKeyedObjectAsync(
        PrefixCapturingStream stream,
        string payloadPrefix,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // System.Text.Json cannot stream a root-level JSON object property-by-property, so the
        // keyed export is buffered. The bound prevents an unexpectedly large/abusive body from
        // exhausting memory; the recent exports are small, and oversized bodies fail closed.
        var body = await ReadAllBoundedAsync(stream, MaxBufferedObjectBytes, payloadPrefix, cancellationToken)
            .ConfigureAwait(false);

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(body, DocumentOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogError(
                ex,
                "ThreatFox keyed-object export is not valid JSON. PayloadPrefix={PayloadPrefix}",
                payloadPrefix);
            throw new ThreatFoxFeedFormatException(
                $"ThreatFox keyed-object export is not valid JSON: {ex.Message}. Payload prefix: {payloadPrefix}",
                payloadPrefix,
                ex);
        }

        using (document)
        {
            var entryIndex = 0;
            foreach (var property in document.RootElement.EnumerateObject())
            {
                entryIndex++;
                var id = property.Name;
                if (property.Value.ValueKind != JsonValueKind.Array)
                {
                    _logger.LogError(
                        "ThreatFox export entry Id={Id} is not an array. PayloadPrefix={PayloadPrefix}",
                        id,
                        payloadPrefix);
                    throw new ThreatFoxFeedFormatException(
                        $"ThreatFox export entry '{id}' is not an array. Payload prefix: {payloadPrefix}",
                        payloadPrefix);
                }

                List<ThreatFoxJsonRecord> entryRecords;
                try
                {
                    entryRecords = property.Value.Deserialize<List<ThreatFoxJsonRecord>>(SerializerOptions)
                        ?? new List<ThreatFoxJsonRecord>();
                }
                catch (JsonException ex)
                {
                    _logger.LogError(
                        ex,
                        "ThreatFox export entry Id={Id} could not be mapped. PayloadPrefix={PayloadPrefix}",
                        id,
                        payloadPrefix);
                    throw new ThreatFoxFeedFormatException(
                        $"ThreatFox export entry '{id}' could not be mapped: {ex.Message}. Payload prefix: {payloadPrefix}",
                        payloadPrefix,
                        ex);
                }

                if (entryRecords.Count > 1)
                {
                    _logger.LogWarning(
                        "ThreatFox export entry Id={Id} contained {RecordCount} records; mapping all (identity collapses to Id)",
                        id,
                        entryRecords.Count);
                }

                foreach (var record in entryRecords)
                {
                    if (record is null)
                    {
                        throw new ThreatFoxRecordParseException(entryIndex, "record", $"Export entry '{id}' contains an empty record.");
                    }

                    yield return string.IsNullOrWhiteSpace(record.Id) ? record with { Id = id } : record;
                }
            }
        }
    }

    private static async Task<byte[]> ReadAllBoundedAsync(
        Stream stream,
        long maxBytes,
        string payloadPrefix,
        CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0)
        {
            if (buffer.Length + read > maxBytes)
            {
                throw new ThreatFoxFeedFormatException(
                    $"ThreatFox keyed-object export exceeds the {maxBytes} byte buffering guard. Payload prefix: {payloadPrefix}",
                    payloadPrefix);
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    private ThreatFoxFeedFormatException BuildShapeFailure(byte? firstToken, string payloadPrefix)
    {
        var firstTokenText = firstToken is null ? "<empty>" : ((char)firstToken.Value).ToString();
        _logger.LogError(
            "ThreatFox feed has an unsupported root token. FirstToken={FirstToken} PayloadPrefix={PayloadPrefix}",
            firstTokenText,
            payloadPrefix);
        return new ThreatFoxFeedFormatException(
            $"ThreatFox feed is neither a JSON array nor a JSON object (first token '{firstTokenText}'). Payload prefix: {payloadPrefix}",
            payloadPrefix);
    }

    private static byte? FirstNonWhitespaceByte(ReadOnlySpan<byte> bytes)
    {
        var span = bytes;
        if (span.StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }))
        {
            span = span[3..];
        }

        foreach (var value in span)
        {
            if (value is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n')
            {
                continue;
            }

            return value;
        }

        return null;
    }

    private static string DescribePrefix(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
        {
            return "<empty body>";
        }

        var text = Encoding.UTF8.GetString(bytes).Replace('\r', ' ').Replace('\n', ' ').Trim();
        return text.Length > 0 ? text : "<non-text body>";
    }
}
