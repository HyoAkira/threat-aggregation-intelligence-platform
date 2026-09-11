namespace ThreatIntelligence.Infrastructure.ThreatFox;

/// <summary>
/// Structured failure raised when a ThreatFox archive is missing its expected entry,
/// contains excessive entries, declares an excessive uncompressed size, or is corrupt/truncated.
/// </summary>
public sealed class ThreatFoxArchiveException(string message, Exception? innerException = null)
    : Exception(message, innerException);

/// <summary>
/// Structured failure raised when a single ThreatFox feed record cannot be mapped.
/// Carries the one-based record index and the offending field so E08 can abort a whole batch
/// without silently dropping the record.
/// </summary>
public sealed class ThreatFoxRecordParseException(int recordIndex, string field, string reason)
    : Exception($"ThreatFox record {recordIndex} rejected in field '{field}': {reason}")
{
    public int RecordIndex { get; } = recordIndex;
    public string Field { get; } = field;
    public string Reason { get; } = reason;
}

/// <summary>
/// Structured failure raised when a ThreatFox export body is not the expected top-level JSON array,
/// for example an upstream status/error object. Carries a capped, sanitized prefix of the raw
/// payload so operators can see what the endpoint actually returned.
/// </summary>
public sealed class ThreatFoxFeedFormatException(string message, string payloadPrefix, Exception? innerException = null)
    : Exception(message, innerException)
{
    public string PayloadPrefix { get; } = payloadPrefix;
}