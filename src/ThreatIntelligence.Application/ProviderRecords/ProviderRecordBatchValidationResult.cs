namespace ThreatIntelligence.Application.ProviderRecords;

public sealed record ProviderRecordBatchValidationResult(
    IReadOnlyList<NormalizedProviderRecord> Records,
    ProviderRecordFailure? Failure)
{
    public bool IsValid => Failure is null;

    public static ProviderRecordBatchValidationResult Success(IReadOnlyList<NormalizedProviderRecord> records)
        => new(records, null);

    public static ProviderRecordBatchValidationResult Failed(
        IReadOnlyList<NormalizedProviderRecord> records,
        ProviderRecordFailure failure)
        => new(records, failure);
}
