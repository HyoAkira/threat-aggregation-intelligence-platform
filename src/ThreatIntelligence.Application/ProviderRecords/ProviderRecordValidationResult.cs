namespace ThreatIntelligence.Application.ProviderRecords;

public sealed record ProviderRecordValidationResult(
    NormalizedProviderRecord? Normalized,
    ProviderRecordFailure? Failure)
{
    public bool IsValid => Normalized is not null;

    public static ProviderRecordValidationResult Success(NormalizedProviderRecord record)
        => new(record, null);

    public static ProviderRecordValidationResult Failed(ProviderRecordFailure failure)
        => new(null, failure);
}