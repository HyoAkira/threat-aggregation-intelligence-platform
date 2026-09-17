using System.Globalization;
using System.Text;
using System.Text.Json;
using ThreatIntelligence.Application.Features.Iocs.Queries;

namespace ThreatIntelligence.Application.AI;

/// <summary>The deterministic evidence snapshot serialized into the model prompt.</summary>
public sealed record IocEvidenceSnapshot(
    int IocId,
    string IocValue,
    string IocType,
    string? UrlHost,
    string? ParentDomain,
    string? FirstSeenUtc,
    string? LastSeenUtc,
    IReadOnlyList<EvidenceSource> Sources,
    int OmittedSourceCount);

public sealed record EvidenceSource(
    string Label,
    string Provider,
    string ProviderRecordId,
    int? Port,
    string? FirstSeenUtc,
    string? LastSeenUtc,
    string? ThreatType,
    string? Malware,
    string? MalwareAlias,
    string? MalwarePrintable,
    bool? IsCompromised,
    bool? IsVerified,
    string? Target,
    string? ConfidenceLevel,
    bool IsFalsePositive,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> PhishTankDetailLines);

/// <summary>
/// Projects a complete <see cref="IocGroupResult"/> into a bounded, deterministic, source-labeled
/// evidence snapshot for the model. Raw provider JSON, provider details links, and reporter
/// identity fields are excluded. Identical input yields an identical snapshot for the duration of
/// the run.
/// </summary>
public static class IocEvidenceProjector
{
    public const int MaxSources = 24;
    public const int MaxDetailLines = 12;
    public const int MaxTagLength = 64;
    public const int MaxStringValueLength = 256;
    public const int MaxTotalBytes = 32_000;

    public static IocEvidenceSnapshot Project(IocGroupResult group) =>
        Project(group, MaxSources, MaxTotalBytes);

    public static IocEvidenceSnapshot Project(IocGroupResult group, int maxSources, int maxTotalBytes)
    {
        var ordered = group.Sources
            .OrderBy(source => source.Provider, StringComparer.Ordinal)
            .ThenBy(source => source.ProviderRecordId, StringComparer.Ordinal)
            .ThenBy(source => source.Port ?? -1)
            .ToList();

        var omitted = Math.Max(0, ordered.Count - maxSources);
        var sources = new List<EvidenceSource>();
        var bytes = 0L;
        foreach (var source in ordered.Take(maxSources))
        {
            var entry = ToSource(source, sources.Count + 1);
            var entryBytes = Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(entry, AiReportJson.Options));
            if (bytes + entryBytes > maxTotalBytes && sources.Count > 0)
            {
                omitted += ordered.Count - sources.Count;
                break;
            }

            bytes += entryBytes;
            sources.Add(entry);
        }

        return new IocEvidenceSnapshot(
            group.Id,
            group.IocValue,
            group.IocType,
            group.UrlHost,
            group.ParentDomain,
            FormatTime(group.FirstSeenUtc),
            FormatTime(group.LastSeenUtc),
            sources,
            omitted);
    }

    /// <summary>The source labels carried by the snapshot, S1..Sn.</summary>
    public static IReadOnlyList<string> SourceLabels(IocEvidenceSnapshot snapshot) =>
        snapshot.Sources.Select(source => source.Label).ToList();

    private static EvidenceSource ToSource(IocSourceResult source, int ordinal)
    {
        var details = source.PhishTankDetails;
        var detailLines = new List<string>();
        if (details.HasValue && details.Value.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in details.Value.EnumerateObject().Take(MaxDetailLines))
            {
                var line = $"\"{Truncate(property.Name, MaxTagLength)}\": \"{Truncate(
                    property.Value.ValueKind == JsonValueKind.String
                        ? property.Value.GetString() ?? string.Empty
                        : property.Value.ToString(), MaxStringValueLength)}\"";
                detailLines.Add(line);
            }
        }

        return new EvidenceSource(
            $"S{ordinal.ToString(CultureInfo.InvariantCulture)}",
            Truncate(source.Provider, MaxStringValueLength),
            Truncate(source.ProviderRecordId, MaxStringValueLength),
            source.Port,
            FormatTime(source.FirstSeenUtc),
            FormatTime(source.LastSeenUtc),
            Truncate(source.ThreatType, MaxStringValueLength),
            Truncate(source.Malware, MaxStringValueLength),
            Truncate(source.MalwareAlias, MaxStringValueLength),
            Truncate(source.MalwarePrintable, MaxStringValueLength),
            source.IsCompromised,
            source.IsVerified,
            Truncate(source.Target, MaxStringValueLength),
            source.ThreatFoxConfidenceLevel?.ToString(CultureInfo.InvariantCulture),
            source.IsFalsePositive,
            source.Tags.Select(tag => Truncate(tag, MaxTagLength)).Take(MaxDetailLines).ToList(),
            detailLines);
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private static string? FormatTime(DateTime? value) =>
        value?.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
}
