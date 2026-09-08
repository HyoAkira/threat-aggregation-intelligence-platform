using System.IO.Compression;
using System.Text.Json;
using ThreatIntelligence.Application.Abstractions;
using ThreatIntelligence.Application.DTOs;
using ThreatIntelligence.Domain.Entities;
using ThreatIntelligence.Domain.Enums;

namespace ThreatIntelligence.Infrastructure.Services;

public class ThreatFoxApiService : IThreatFoxApiService
{
    private const string BaseUrl = "https://threatfox-api.abuse.ch";
    private readonly HttpClient _httpClient;
    private readonly IocNormalizer _normalizer;
    private readonly IocPersistenceService _persistenceService;

    public ThreatFoxApiService(
        HttpClient httpClient,
        IocNormalizer normalizer,
        IocPersistenceService persistenceService)
    {
        _httpClient = httpClient;
        _normalizer = normalizer;
        _persistenceService = persistenceService;
    }

    public async Task<DownloadDatasetResponse> DownloadDatasetAsync(CancellationToken cancellationToken = default)
    {
        var apiKey = GetApiKey();
        using var response = await _httpClient.GetAsync($"{BaseUrl}/v2/files/exports/{apiKey}/full.json.zip", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        await using var zipStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: true);
        var entry = archive.GetEntry("full.json") ?? throw new InvalidDataException("Could not find 'full.json' inside the zip archive.");
        await using var entryStream = entry.Open();
        using var document = JsonDocument.Parse(entryStream);

        var records = ParseJsonResponse(document);
        await _persistenceService.PersistAsync("ThreatFox", records, cancellationToken);
        return new DownloadDatasetResponse(records.Count, "Download completed", DateTime.UtcNow);
    }

    public async Task<GetRecentIoCsResponse> RecentIoCsAsync(CancellationToken cancellationToken = default)
    {
        var apiKey = GetApiKey();
        using var response = await _httpClient.GetAsync($"{BaseUrl}/v2/files/exports/{apiKey}/recent.json", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = JsonDocument.Parse(stream);
        var records = ParseJsonResponse(document);
        await _persistenceService.PersistAsync("ThreatFox", records, cancellationToken);
        return new GetRecentIoCsResponse(records.Count, "Recent IOCs fetched", DateTime.UtcNow);
    }

    private string GetApiKey()
    {
        var apiKey = Environment.GetEnvironmentVariable("AuthKey__AbuseCh");
        return string.IsNullOrWhiteSpace(apiKey)
            ? throw new InvalidOperationException("ThreatFox API key (AuthKey:AbuseCh) is not configured.")
            : apiKey.Trim();
    }

    private async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException($"ThreatFox API returned {(int)response.StatusCode}: {errorBody}");
        }
    }

    internal List<ProviderIoc> ParseJsonResponse(JsonDocument document)
    {
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("ThreatFox API response is not a JSON object.");
        }

        var records = new List<ProviderIoc>();
        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (!int.TryParse(property.Name, out var recordId) || recordId <= 0)
            {
                throw new InvalidDataException($"ThreatFox record id '{property.Name}' is invalid.");
            }

            if (property.Value.ValueKind == JsonValueKind.Array)
            {
                records.AddRange(property.Value.EnumerateArray().Select(element => MapRecord(element, recordId)));
            }
            else
            {
                records.Add(MapRecord(property.Value, recordId));
            }
        }

        return records.Count == 0
            ? throw new InvalidDataException("ThreatFox API response contained no IOC entries.")
            : records;
    }

    internal ProviderIoc MapRecord(JsonElement element, int recordId)
    {
        var rawValue = RequiredString(element, "ioc_value");
        var rawType = RequiredString(element, "ioc_type");
        var normalized = _normalizer.Normalize(rawValue, GetExpectedType(rawType));
        var now = DateTime.UtcNow;
        var ioc = new Ioc
        {
            ProviderRecordId = recordId.ToString(),
            ProviderDetailsLink = OptionalString(element, "reference"),
            IocValue = normalized.IocValue,
            IocValueHash = normalized.IocValueHash,
            IocType = normalized.IocType,
            Port = normalized.Port,
            UrlHost = normalized.UrlHost,
            ParentDomain = normalized.ParentDomain,
            ThreatType = OptionalString(element, "threat_type"),
            Malware = OptionalString(element, "malware"),
            MalwareAlias = OptionalString(element, "malware_alias"),
            MalwarePrintable = OptionalString(element, "malware_printable"),
            FirstSeenUtc = RequiredDate(element, "first_seen_utc"),
            LastSeenUtc = OptionalDate(element, "last_seen_utc"),
            ThreatFoxConfidenceLevel = OptionalByte(element, "confidence_level"),
            IsCompromised = OptionalBoolean(element, "is_compromised"),
            Anonymous = OptionalBoolean(element, "anonymous"),
            Reporter = OptionalString(element, "reporter"),
            CreatedAt = now,
            UpdatedAt = now
        };

        return new ProviderIoc(ioc, SplitTags(OptionalString(element, "tags")));
    }

    private static IocType? GetExpectedType(string value) => value.ToLowerInvariant() switch
    {
        "url" => IocType.Url,
        "domain" => IocType.Domain,
        "ipv4" => IocType.Ipv4,
        "ipv6" => IocType.Ipv6,
        _ => null
    };

    private static string RequiredString(JsonElement element, string property) =>
        OptionalString(element, property) ?? throw new InvalidDataException($"ThreatFox record is missing '{property}'.");

    private static string? OptionalString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static DateTime RequiredDate(JsonElement element, string property) =>
        OptionalDate(element, property) ?? throw new InvalidDataException($"ThreatFox record has an invalid '{property}'.");

    private static DateTime? OptionalDate(JsonElement element, string property) =>
        OptionalString(element, property) is { } value && DateTime.TryParse(value, out var date) ? date : null;

    private static byte? OptionalByte(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetByte(out var number) ? number : null;

    private static bool? OptionalBoolean(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number when value.TryGetInt32(out var number) => number != 0,
            JsonValueKind.String => bool.TryParse(value.GetString(), out var result) ? result : null,
            _ => null
        };
    }

    private static IReadOnlyCollection<string> SplitTags(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Select(tag => tag.ToLowerInvariant())
                .Distinct(StringComparer.Ordinal)
                .ToArray();
}
