namespace ThreatIntelligence.Application.ProviderRecords;

public enum ProviderRecordFailureCode : byte
{
    InvalidProviderId,
    BlankProviderRecordId,
    ProviderRecordIdTooLong,
    BlankRawIoc,
    InvalidIoc,
    TypeMismatch,
    InvalidTimestamp,
    InvalidDetailsLink,
    DetailsLinkTooLong,
    InvalidTag,
    UnsupportedProvider,
    ProviderRejected
}