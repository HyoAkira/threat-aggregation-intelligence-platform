namespace ThreatIntelligence.Application.ProviderRecords;

public static class ProviderRecordErrorMessages
{
    public const string InvalidProviderId = "Provider ID must be positive.";
    public const string BlankProviderRecordId = "Provider record ID must not be blank.";
    public const string ProviderRecordIdTooLong = "Provider record ID must not exceed 100 characters.";
    public const string BlankRawIoc = "Raw IOC must not be blank.";
    public const string InvalidIoc = "Raw IOC is invalid.";
    public const string TypeMismatch = "Declared IOC type does not match the normalized type.";
    public const string InvalidTimestamp = "Provider timestamp must be in UTC.";
    public const string InvalidDetailsLink = "Provider details link must be an absolute HTTP or HTTPS URL.";
    public const string DetailsLinkTooLong = "Provider details link must not exceed 2048 characters.";
    public const string InvalidTag = "Tag must not exceed 100 characters.";
    public const string UnsupportedProvider = "No validator is registered for this provider.";
    public const string ProviderRejected = "Provider-specific payload validation failed.";
}