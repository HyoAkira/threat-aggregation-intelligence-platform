using System.IO.Compression;
using System.Text.Json;
using ThreatIntelligence.Application.Abstractions;
using ThreatIntelligence.Application.DTOs;
using ThreatIntelligence.Domain.Entities;
using ThreatIntelligence.Domain.Enums;

namespace ThreatIntelligence.Infrastructure.Services;

public class PhishTankApiService : IPhishTankApiService
{
    private readonly HttpClient _httpClient;
    private readonly IocNormalizer _normalizer;
    private readonly IocPersistenceService _persistenceService;

    public PhishTankApiService(
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
        using var response = await _httpClient.GetAsync("online-valid.json.gz", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException($"PhishTank API returned {(int)response.StatusCode}: {errorBody}");
        }

        await using var compressedStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var gzipStream = new GZipStream(compressedStream, CompressionMode.Decompress, leaveOpen: true);
        using var document = JsonDocument.Parse(gzipStream);
        var records = ParseJsonArray(document);

        await _persistenceService.PersistAsync("PhishTank", records, cancellationToken);
        return new DownloadDatasetResponse(records.Count, "Download completed", DateTime.UtcNow);
    }

    internal List<ProviderIoc> ParseJsonArray(JsonDocument document)
    {
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("PhishTank API response is not a JSON array.");
        }

        var records = document.RootElement.EnumerateArray().Select(MapRecord).ToList();
        return records.Count == 0
            ? throw new InvalidDataException("PhishTank API response contained no phish entries.")
            : records;
    }

    internal ProviderIoc MapRecord(JsonElement element)
    {
        var phishId = RequiredInt(element, "phish_id");
        var normalized = _normalizer.Normalize(RequiredString(element, "url"), IocType.Url);
        var now = DateTime.UtcNow;
        var details = OptionalDetails(element);

        var ioc = new Ioc
        {
            ProviderRecordId = phishId.ToString(),
            ProviderDetailsLink = OptionalString(element, "phish_detail_url"),
            IocValue = normalized.IocValue,
            IocValueHash = normalized.IocValueHash,
            IocType = normalized.IocType,
            Port = normalized.Port,
            UrlHost = normalized.UrlHost,
            ParentDomain = normalized.ParentDomain,
            FirstSeenUtc = RequiredDate(element, "submission_time"),
            LastSeenUtc = OptionalDate(element, "verification_time"),
            IsVerified = YesNoBoolean(element, "verified"),
            PhishTankOnline = YesNoBoolean(element, "online"),
            Target = OptionalString(element, "target"),
            PhishTankDetails = details,
            CreatedAt = now,
            UpdatedAt = now
        };

        return new ProviderIoc(ioc, []);
    }

    private static int RequiredInt(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) && number > 0
            ? number
            : throw new InvalidDataException($"PhishTank record has an invalid '{property}'.");

    private static string RequiredString(JsonElement element, string property) =>
        OptionalString(element, property) ?? throw new InvalidDataException($"PhishTank record is missing '{property}'.");

    private static string? OptionalString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static DateTime RequiredDate(JsonElement element, string property) =>
        OptionalDate(element, property) ?? throw new InvalidDataException($"PhishTank record has an invalid '{property}'.");

    private static DateTime? OptionalDate(JsonElement element, string property) =>
        OptionalString(element, property) is { } value && DateTime.TryParse(value, out var date) ? date : null;

    private static bool? YesNoBoolean(JsonElement element, string property) =>
        OptionalString(element, property) is { } value
            ? value.Equals("yes", StringComparison.OrdinalIgnoreCase)
            : null;

    private static string? OptionalDetails(JsonElement element)
    {
        if (!element.TryGetProperty("details", out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.Array
            ? value.GetRawText()
            : throw new InvalidDataException("PhishTank record has invalid details JSON.");
    }
}
