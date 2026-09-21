namespace ThreatIntelligence.Infrastructure.PhishTank;

public sealed class PhishTankDecompressionException(string message, Exception? innerException = null)
    : Exception(message, innerException);

public sealed class PhishTankRecordParseException(int recordIndex, string field, string reason)
    : Exception($"PhishTank record {recordIndex} rejected in field '{field}': {reason}")
{
    public int RecordIndex { get; } = recordIndex;
    public string Field { get; } = field;
    public string Reason { get; } = reason;
}

public sealed class PhishTankQuotaException(string message)
    : Exception(message);

public sealed class PhishTankAuthenticationException(string message)
    : Exception(message);
