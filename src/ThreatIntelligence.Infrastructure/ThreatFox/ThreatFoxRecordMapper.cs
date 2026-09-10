using System.Globalization;
using ThreatIntelligence.Application.ProviderRecords;
using ThreatIntelligence.Domain.Enums;

namespace ThreatIntelligence.Infrastructure.ThreatFox;

/// <summary>
/// Maps a parsed ThreatFox JSON record into the canonical E03 <see cref="ProviderRecordInput"/>,
/// preserving valid <c>false</c>, <c>0</c>, and empty values while leaving absent values null.
/// </summary>
public sealed class ThreatFoxRecordMapper
{
    public const int ThreatFoxProviderId = 1;

    public const string ThreatTypeKey = "threat_type";
    public const string ThreatTypeDescKey = "threat_type_desc";
    public const string IocTypeDescKey = "ioc_type_desc";
    public const string MalwareKey = "malware";
    public const string MalwarePrintableKey = "malware_printable";
    public const string MalwareAliasKey = "malware_alias";
    public const string MalwareMalpediaKey = "malware_malpedia";
    public const string ConfidenceLevelKey = "confidence_level";
    public const string IsCompromisedKey = "is_compromised";
    public const string ReporterKey = "reporter";
    public const string ReferenceKey = "reference";
    public const string IocTypeKey = "ioc_type";
    public const string MalwareSamplesKey = "malware_samples";

    public ProviderRecordInput Map(ThreatFoxJsonRecord record, int recordIndex)
    {
        if (record is null)
        {
            throw new ThreatFoxRecordParseException(recordIndex, "record", "Empty record in feed.");
        }

        var id = Require(record.Id, "id", recordIndex);
        var ioc = Require(record.Ioc, "ioc", recordIndex);
        var type = MapIocType(record.IocType, recordIndex);
        var firstSeen = ParseTimestamp(record.FirstSeen, "first_seen", recordIndex);
        var lastSeen = ParseOptionalTimestamp(record.LastSeen, "last_seen", recordIndex);

        return new ProviderRecordInput(
            ProviderId: ThreatFoxProviderId,
            ProviderRecordId: id,
            RawIoc: ioc,
            DeclaredType: type,
            FirstSeenAt: firstSeen,
            LastSeenAt: lastSeen,
            ProviderDetailsLink: BuildDetailsLink(id),
            Tags: NormalizeTags(record.Tags),
            Metadata: BuildMetadata(record),
            Payload: null);
    }

    /// <summary>
    /// Maps the ThreatFox <c>ioc_type</c> string to an <see cref="IocType"/>.
    /// <c>ip:port</c> maps to null so the classifier decides between IPv4 and IPv6.
    /// </summary>
    private static IocType? MapIocType(string? iocType, int recordIndex) => (iocType ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "domain" => IocType.Domain,
        "url" => IocType.Url,
        "ip:port" => null,
        _ => throw new ThreatFoxRecordParseException(recordIndex, "ioc_type", $"Unsupported IOC type '{iocType}'.")
    };

    private static string Require(string? value, string field, int recordIndex)
        => string.IsNullOrWhiteSpace(value)
            ? throw new ThreatFoxRecordParseException(recordIndex, field, "Value is blank.")
            : value.Trim();

    private static DateTime ParseTimestamp(string? value, string field, int recordIndex)
        => TryParseThreatFoxTimestamp(value, out var utc)
            ? utc
            : throw new ThreatFoxRecordParseException(recordIndex, field, $"Timestamp '{value}' is not a valid UTC value.");

    private static DateTime? ParseOptionalTimestamp(string? value, string field, int recordIndex)
        => string.IsNullOrWhiteSpace(value)
            ? null
            : ParseTimestamp(value, field, recordIndex);

    private static bool TryParseThreatFoxTimestamp(string? value, out DateTime utc)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            utc = default;
            return false;
        }

        var normalized = value.Trim();
        if (normalized.EndsWith("UTC", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[..^3].Trim();
        }
        else if (normalized.EndsWith('Z'))
        {
            normalized = normalized[..^1].Trim();
        }
        else if (normalized.EndsWith('z'))
        {
            normalized = normalized[..^1].Trim();
        }

        return DateTime.TryParse(
            normalized,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out utc);
    }

    private static string BuildDetailsLink(string id)
        => $"https://threatfox.abuse.ch/ioc/{id}";

    private static IReadOnlyCollection<string?>? NormalizeTags(IReadOnlyList<string>? tags)
    {
        if (tags is null || tags.Count == 0)
        {
            return null;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var normalized = new List<string?>(tags.Count);
        foreach (var tag in tags)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                continue;
            }

            var value = tag.Trim().ToLowerInvariant();
            if (seen.Add(value))
            {
                normalized.Add(value);
            }
        }

        return normalized.Count > 0 ? normalized : null;
    }

    private static IReadOnlyDictionary<string, object?> BuildMetadata(ThreatFoxJsonRecord record)
    {
        var metadata = new Dictionary<string, object?>
        {
            [IocTypeKey] = record.IocType
        };

        AddIfNotNull(metadata, ThreatTypeKey, record.ThreatType);
        AddIfNotNull(metadata, ThreatTypeDescKey, record.ThreatTypeDesc);
        AddIfNotNull(metadata, IocTypeDescKey, record.IocTypeDesc);
        AddIfNotNull(metadata, MalwareKey, record.Malware);
        AddIfNotNull(metadata, MalwarePrintableKey, record.MalwarePrintable);
        AddIfNotNull(metadata, MalwareAliasKey, record.MalwareAlias);
        AddIfNotNull(metadata, MalwareMalpediaKey, record.MalwareMalpedia);
        AddIfNotNull(metadata, ConfidenceLevelKey, record.ConfidenceLevel);
        AddIfNotNull(metadata, IsCompromisedKey, record.IsCompromised);
        AddIfNotNull(metadata, ReporterKey, record.Reporter);
        AddIfNotNull(metadata, ReferenceKey, record.Reference);
        AddIfNotNull(metadata, MalwareSamplesKey, record.MalwareSamples);

        return metadata;
    }

    private static void AddIfNotNull(Dictionary<string, object?> metadata, string key, object? value)
    {
        if (value is not null)
        {
            metadata[key] = value;
        }
    }
}