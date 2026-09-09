namespace ThreatIntelligence.Application.ProviderRecords;

public class ProviderRecordFailure
{
    public const int MaxProviderRecordIdLength = 100;
    public const int MaxMessageLength = 512;

    private ProviderRecordFailure(
        int recordIndex,
        int providerId,
        string providerRecordId,
        ProviderRecordFailureCode code,
        string field,
        string message)
    {
        RecordIndex = recordIndex;
        ProviderId = providerId;
        ProviderRecordId = providerRecordId;
        Code = code;
        Field = field;
        Message = message;
    }

    public int RecordIndex { get; }
    public int ProviderId { get; }
    public string ProviderRecordId { get; }
    public ProviderRecordFailureCode Code { get; }
    public string Field { get; }
    public string Message { get; }

    public static ProviderRecordFailure Create(
        int recordIndex,
        int providerId,
        string? providerRecordId,
        ProviderRecordFailureCode code,
        string field,
        string message)
        => new(
            recordIndex,
            providerId,
            Truncate(providerRecordId, MaxProviderRecordIdLength),
            code,
            field,
            Truncate(message, MaxMessageLength));

    private static string Truncate(string? value, int maxLength)
        => string.IsNullOrEmpty(value)
            ? string.Empty
            : value.Length <= maxLength ? value : value[..maxLength];
}
