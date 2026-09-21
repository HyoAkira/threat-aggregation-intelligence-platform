using System.Globalization;
using System.Text.Json;
using ThreatIntelligence.Application.ProviderRecords;
using ThreatIntelligence.Domain.Enums;

namespace ThreatIntelligence.Infrastructure.PhishTank;

public sealed class PhishTankRecordMapper
{
    public const string VerifiedKey = "verified";
    public const string OnlineKey = "online";
    public const string TargetKey = "target";
    public const string PhishTankDetailsKey = "phishTankDetails";

    private static readonly JsonSerializerOptions s_detailsJsonOptions = new()
    {
        WriteIndented = false
    };

    public ProviderRecordInput Map(PhishTankFeedEntry record, int recordIndex)
    {
        if (record is null) throw new PhishTankRecordParseException(recordIndex, "record", "Empty record in feed.");

        var phishId = RequirePhishId(record.PhishId, recordIndex);
        var url = RequireUrl(record.Url, recordIndex);
        var firstSeen = ParseTimestamp(record.SubmissionTime, "submission_time", recordIndex);
        var lastSeen = ParseOptionalTimestamp(record.VerificationTime, "verification_time", recordIndex);
        var detailsJson = SerializeDetails(record.Details, recordIndex);

        return new ProviderRecordInput(
            ProviderId: PhishTankProviderValidator.PhishTankProviderId,
            ProviderRecordId: phishId.ToString(CultureInfo.InvariantCulture),
            RawIoc: url,
            DeclaredType: IocType.Url,
            FirstSeenAt: firstSeen,
            LastSeenAt: lastSeen,
            ProviderDetailsLink: record.PhishDetailUrl,
            Tags: null,
            Metadata: BuildMetadata(record),
            Payload: detailsJson is not null
                ? new Dictionary<string, object?> { [PhishTankDetailsKey] = detailsJson }
                : null);
    }

    private static int RequirePhishId(int? phishId, int recordIndex)
    {
        if (phishId is null or <= 0)
            throw new PhishTankRecordParseException(recordIndex, "phish_id", "PhishTank phish_id must be a positive integer.");
        return phishId.Value;
    }

    private static string RequireUrl(string? url, int recordIndex)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new PhishTankRecordParseException(recordIndex, "url", "PhishTank url must not be blank.");
        return url.Trim();
    }

    private static DateTime ParseTimestamp(string? value, string field, int recordIndex)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new PhishTankRecordParseException(recordIndex, field, $"PhishTank {field} must not be blank.");

        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dto))
            return dto.UtcDateTime;

        throw new PhishTankRecordParseException(recordIndex, field, $"PhishTank {field} is not a valid timestamp: '{value}'.");
    }

    private static DateTime? ParseOptionalTimestamp(string? value, string field, int recordIndex)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dto))
            return dto.UtcDateTime;

        throw new PhishTankRecordParseException(recordIndex, field, $"PhishTank {field} is not a valid timestamp: '{value}'.");
    }

    private static bool? ParseYesNo(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "yes" => true,
        "no" => false,
        "" or null => null,
        _ => null
    };

    private static Dictionary<string, object?> BuildMetadata(PhishTankFeedEntry record)
    {
        var metadata = new Dictionary<string, object?>();
        var verified = ParseYesNo(record.Verified);
        if (verified is not null) metadata[VerifiedKey] = verified;
        var online = ParseYesNo(record.Online);
        if (online is not null) metadata[OnlineKey] = online;
        if (record.Target is not null) metadata[TargetKey] = record.Target;
        return metadata;
    }

    private static string? SerializeDetails(IReadOnlyList<PhishTankDetailEntry>? details, int recordIndex)
    {
        if (details is null || details.Count == 0)
            return null;

        try
        {
            return JsonSerializer.Serialize(details, s_detailsJsonOptions);
        }
        catch (JsonException ex)
        {
            throw new PhishTankRecordParseException(recordIndex, "details", $"Failed to serialize details: {ex.Message}");
        }
    }
}
