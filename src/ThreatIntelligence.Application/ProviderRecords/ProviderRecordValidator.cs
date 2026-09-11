using ThreatIntelligence.Application.Normalization;

namespace ThreatIntelligence.Application.ProviderRecords;

public sealed class ProviderRecordValidator
{
    public const int MaxDetailsLinkLength = 2048;
    public const int MaxTagLength = 100;

    private readonly IIocClassifier _classifier;
    private readonly IReadOnlyDictionary<int, IProviderValidator> _validators;

    public ProviderRecordValidator(IIocClassifier classifier, IReadOnlyDictionary<int, IProviderValidator> validators)
    {
        _classifier = classifier;
        _validators = validators;
    }

    public ProviderRecordValidationResult Validate(ProviderRecordInput input, int recordIndex)
    {
        var providerRecordId = input.ProviderRecordId;
        if (input.ProviderId <= 0)
        {
            return Failure(input, recordIndex, ProviderRecordFailureCode.InvalidProviderId,
                "providerId", ProviderRecordErrorMessages.InvalidProviderId);
        }

        if (string.IsNullOrWhiteSpace(providerRecordId))
        {
            return Failure(input, recordIndex, ProviderRecordFailureCode.BlankProviderRecordId,
                "providerRecordId", ProviderRecordErrorMessages.BlankProviderRecordId);
        }

        if (providerRecordId.Length > ProviderRecordFailure.MaxProviderRecordIdLength)
        {
            return Failure(input, recordIndex, ProviderRecordFailureCode.ProviderRecordIdTooLong,
                "providerRecordId", ProviderRecordErrorMessages.ProviderRecordIdTooLong);
        }

        var rawIoc = input.RawIoc;
        if (string.IsNullOrWhiteSpace(rawIoc))
        {
            return Failure(input, recordIndex, ProviderRecordFailureCode.BlankRawIoc,
                "rawIoc", ProviderRecordErrorMessages.BlankRawIoc);
        }

        var classification = _classifier.Classify(rawIoc);
        if (!classification.IsSuccess)
        {
            return Failure(input, recordIndex, ProviderRecordFailureCode.InvalidIoc,
                "ioc", ProviderRecordErrorMessages.InvalidIoc);
        }

        var ioc = classification.OrThrow();
        if (input.DeclaredType is not null && input.DeclaredType != ioc.Type)
        {
            return Failure(input, recordIndex, ProviderRecordFailureCode.TypeMismatch,
                "ioc", ProviderRecordErrorMessages.TypeMismatch);
        }

        if (input.FirstSeenAt.Kind != DateTimeKind.Utc)
        {
            return Failure(input, recordIndex, ProviderRecordFailureCode.InvalidTimestamp,
                "firstSeenAt", ProviderRecordErrorMessages.InvalidTimestamp);
        }

        if (input.LastSeenAt is { Kind: not DateTimeKind.Utc })
        {
            return Failure(input, recordIndex, ProviderRecordFailureCode.InvalidTimestamp,
                "lastSeenAt", ProviderRecordErrorMessages.InvalidTimestamp);
        }

        if (input.ProviderDetailsLink is not null)
        {
            if (input.ProviderDetailsLink.Length > MaxDetailsLinkLength)
            {
                return Failure(input, recordIndex, ProviderRecordFailureCode.DetailsLinkTooLong,
                    "providerDetailsLink", ProviderRecordErrorMessages.DetailsLinkTooLong);
            }

            if (!TryValidateDetailsLink(input.ProviderDetailsLink))
            {
                return Failure(input, recordIndex, ProviderRecordFailureCode.InvalidDetailsLink,
                    "providerDetailsLink", ProviderRecordErrorMessages.InvalidDetailsLink);
            }
        }

        if (!TryNormalizeTags(input.Tags, out var tags))
        {
            return Failure(input, recordIndex, ProviderRecordFailureCode.InvalidTag,
                "tags", ProviderRecordErrorMessages.InvalidTag);
        }

        if (!_validators.TryGetValue(input.ProviderId, out var providerValidator))
        {
            return Failure(input, recordIndex, ProviderRecordFailureCode.UnsupportedProvider,
                "provider", ProviderRecordErrorMessages.UnsupportedProvider);
        }

        var rejection = providerValidator.Validate(input);
        if (rejection is not null)
        {
            return Failure(input, recordIndex, ProviderRecordFailureCode.ProviderRejected,
                "payload", ProviderRecordErrorMessages.ProviderRejected);
        }

        var record = new NormalizedProviderRecord(
            input.ProviderId,
            providerRecordId,
            ioc,
            new DateTimeOffset(input.FirstSeenAt),
            input.LastSeenAt is { } last ? new DateTimeOffset(last) : null,
            input.ProviderDetailsLink,
            tags,
            input.Metadata,
            input.Payload);

        return ProviderRecordValidationResult.Success(record);
    }

    public ProviderRecordBatchValidationResult ValidateBatch(IEnumerable<ProviderRecordInput> inputs)
    {
        var records = new List<NormalizedProviderRecord>();
        var recordIndex = 0;

        foreach (var input in inputs)
        {
            recordIndex++;
            var result = Validate(input, recordIndex);
            if (!result.IsValid)
            {
                return ProviderRecordBatchValidationResult.Failed(records, result.Failure!);
            }

            records.Add(result.Normalized!);
        }

        return ProviderRecordBatchValidationResult.Success(records);
    }

    private static ProviderRecordValidationResult Failure(
        ProviderRecordInput input,
        int recordIndex,
        ProviderRecordFailureCode code,
        string field,
        string message)
    {
        return ProviderRecordValidationResult.Failed(
            ProviderRecordFailure.Create(recordIndex, input.ProviderId, input.ProviderRecordId, code, field, message));
    }

    private static bool TryValidateDetailsLink(string link)
    {
        if (!Uri.TryCreate(link, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        if (uri.UserInfo.Length > 0)
        {
            return false;
        }

        return true;
    }

    private static bool TryNormalizeTags(IReadOnlyCollection<string?>? rawTags, out IReadOnlyList<string> normalized)
    {
        normalized = Array.Empty<string>();
        if (rawTags is null || rawTags.Count == 0)
        {
            return true;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<string>(rawTags.Count);
        foreach (var rawTag in rawTags)
        {
            if (string.IsNullOrWhiteSpace(rawTag))
            {
                continue;
            }

            var tag = rawTag.Trim().ToLowerInvariant();
            if (tag.Length > MaxTagLength)
            {
                return false;
            }

            if (seen.Add(tag))
            {
                result.Add(tag);
            }
        }

        normalized = result;
        return true;
    }
}
