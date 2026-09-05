using System.IO.Compression;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ThreatIntelligence.Application.Abstractions;
using ThreatIntelligence.Domain.Entities;

namespace ThreatIntelligence.Infrastructure.Services;

public class ThreatFoxApiService : IThreatFoxApiService
{
    private readonly ThreatIntelDbContext _dbContext;
    private readonly HttpClient _httpClient;
    private const string BaseUrl = "https://threatfox-api.abuse.ch";

    public ThreatFoxApiService(ThreatIntelDbContext dbContext, HttpClient httpClient)
    {
        _dbContext = dbContext;
        _httpClient = httpClient;
    }

    public async Task<int> DownloadDatasetAsync(CancellationToken cancellationToken = default)
    {
        var apiKey = GetApiKey();

        var url = $"{BaseUrl}/v2/files/exports/{apiKey}/full.json.zip";

        using var response = await _httpClient.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException($"ThreatFox API returned {(int)response.StatusCode}: {errorBody}");
        }

        var zipStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var zipArchive = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: true);

        var entry = zipArchive.GetEntry("full.json");
        if (entry == null)
        {
            throw new InvalidDataException("Could not find 'full.json' inside the zip archive.");
        }

        using var entryStream = entry.Open();
        using var doc = JsonDocument.Parse(entryStream);

        var entities = ParseJsonResponse(doc);
        foreach (var entity in entities)
        {
            await UpsertThreatFoxAsync(entity, cancellationToken);
        }

        return entities.Count;
    }

    public async Task<int> RecentIoCsAsync(CancellationToken cancellationToken = default)
    {
        var apiKey = GetApiKey();

        var url = $"{BaseUrl}/v2/files/exports/{apiKey}/recent.json";

        using var response = await _httpClient.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException($"ThreatFox API returned {(int)response.StatusCode}: {errorBody}");
        }

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = JsonDocument.Parse(stream);

        var entities = ParseJsonResponse(doc);
        foreach (var entity in entities)
        {
            await UpsertThreatFoxAsync(entity, cancellationToken);
        }

        return entities.Count;
    }

    private string GetApiKey()
    {
        var apiKey = Environment.GetEnvironmentVariable("AuthKey__AbuseCh");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException($"ThreatFox API key (AuthKey:AbuseCh) is not configured.");
        }
        return apiKey.Trim();
    }

    private List<ThreatFox> ParseJsonResponse(JsonDocument doc)
    {
        var entities = new List<ThreatFox>();

        if (doc.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("ThreatFox API response is not a JSON object.");
        }

        foreach (var property in doc.RootElement.EnumerateObject())
        {
            var iocId = int.TryParse(property.Name, out var parsedId) ? parsedId : 0;

            if (property.Value.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in property.Value.EnumerateArray())
                {
                    var entity = MapJsonToEntity(element, iocId);
                    entities.Add(entity);
                }
            }
            else
            {
                var entity = MapJsonToEntity(property.Value, iocId);
                entities.Add(entity);
            }
        }

        if (entities.Count == 0)
        {
            throw new InvalidDataException("ThreatFox API response contained no IOC entries.");
        }

        return entities;
    }

    private ThreatFox MapJsonToEntity(JsonElement element, int iocId)
    {
        var entity = new ThreatFox
        {
            IocId = iocId
        };

        var iocValueResult = element.TryGetProperty("ioc_value", out var valueProp) && valueProp.ValueKind == JsonValueKind.String
            ? valueProp.GetString()
            : null;
        entity.IocValue = iocValueResult ?? string.Empty;

        entity.IocType = element.TryGetProperty("ioc_type", out var typeProp) && typeProp.ValueKind == JsonValueKind.String
            ? typeProp.GetString() ?? string.Empty
            : string.Empty;

        entity.ThreatType = element.TryGetProperty("threat_type", out var threatTypeProp) && threatTypeProp.ValueKind == JsonValueKind.String
            ? threatTypeProp.GetString() ?? string.Empty
            : string.Empty;

        entity.Malware = element.TryGetProperty("malware", out var malwareProp) && malwareProp.ValueKind == JsonValueKind.String
            ? malwareProp.GetString() ?? string.Empty
            : string.Empty;

        entity.MalwareAlias = element.TryGetProperty("malware_alias", out var aliasProp) && aliasProp.ValueKind == JsonValueKind.String
            ? aliasProp.GetString()
            : null;

        entity.MalwarePrintable = element.TryGetProperty("malware_printable", out var printableProp) && printableProp.ValueKind == JsonValueKind.String
            ? printableProp.GetString() ?? string.Empty
            : string.Empty;

        entity.FirstSeenUtc = element.TryGetProperty("first_seen_utc", out var firstSeenProp) && firstSeenProp.ValueKind == JsonValueKind.String && DateTime.TryParse(firstSeenProp.GetString(), out var firstSeen)
            ? firstSeen
            : DateTime.UtcNow;

        entity.LastSeenUtc = element.TryGetProperty("last_seen_utc", out var lastSeenProp) && lastSeenProp.ValueKind == JsonValueKind.String && DateTime.TryParse(lastSeenProp.GetString(), out var lastSeen)
            ? lastSeen
            : null;

        entity.ConfidenceLevel = element.TryGetProperty("confidence_level", out var confidenceProp) && confidenceProp.ValueKind == JsonValueKind.Number
            ? confidenceProp.GetInt32()
            : 0;

        entity.IsCompromised = element.TryGetProperty("is_compromised", out var compromisedProp) && compromisedProp.ValueKind == JsonValueKind.True;

        entity.Reference = element.TryGetProperty("reference", out var refProp) && refProp.ValueKind == JsonValueKind.String
            ? refProp.GetString()
            : null;

        entity.Tags = element.TryGetProperty("tags", out var tagProp) && tagProp.ValueKind == JsonValueKind.String
            ? tagProp.GetString()
            : null;

        entity.Anonymous = element.TryGetProperty("anonymous", out var anonymousProp) && anonymousProp.ValueKind == JsonValueKind.Number
            ? anonymousProp.GetInt32()
            : 0;

        var reporterResult = element.TryGetProperty("reporter", out var reporterProp) && reporterProp.ValueKind == JsonValueKind.String
            ? reporterProp.GetString()
            : null;
        entity.Reporter = reporterResult ?? string.Empty;

        return entity;
    }

    private async Task UpsertThreatFoxAsync(ThreatFox entity, CancellationToken cancellationToken)
    {
        var sql = @"
MERGE INTO [providers].[ThreatFox] AS target
USING (VALUES (@P0, @P1, @P2, @P3, @P4, @P5, @P6, @P7, @P8, @P9, @P10, @P11, @P12, @P13, @P14)) 
AS source (IocId, IocValue, IocType, ThreatType, Malware, MalwareAlias, MalwarePrintable, FirstSeenUtc, LastSeenUtc, ConfidenceLevel, IsCompromised, Reference, Tags, Anonymous, Reporter)
ON target.IocId = source.IocId AND target.IocValue = source.IocValue AND target.IocType = source.IocType
WHEN MATCHED THEN
    UPDATE SET
        ThreatType = source.ThreatType,
        Malware = source.Malware,
        MalwareAlias = source.MalwareAlias,
        MalwarePrintable = source.MalwarePrintable,
        FirstSeenUtc = source.FirstSeenUtc,
        LastSeenUtc = source.LastSeenUtc,
        ConfidenceLevel = source.ConfidenceLevel,
        IsCompromised = source.IsCompromised,
        Reference = source.Reference,
        Tags = source.Tags,
        Anonymous = source.Anonymous,
        Reporter = source.Reporter
WHEN NOT MATCHED THEN
    INSERT (IocId, IocValue, IocType, ThreatType, Malware, MalwareAlias, MalwarePrintable, FirstSeenUtc, LastSeenUtc, ConfidenceLevel, IsCompromised, Reference, Tags, Anonymous, Reporter)
    VALUES (source.IocId, source.IocValue, source.IocType, source.ThreatType, source.Malware, source.MalwareAlias, source.MalwarePrintable, source.FirstSeenUtc, source.LastSeenUtc, source.ConfidenceLevel, source.IsCompromised, source.Reference, source.Tags, source.Anonymous, source.Reporter);";

        var parameters = new object?[]
        {
            entity.IocId,
            entity.IocValue!,
            entity.IocType!,
            entity.ThreatType!,
            entity.Malware!,
            entity.MalwareAlias,
            entity.MalwarePrintable!,
            entity.FirstSeenUtc,
            entity.LastSeenUtc,
            entity.ConfidenceLevel,
            entity.IsCompromised,
            entity.Reference,
            entity.Tags,
            entity.Anonymous,
            entity.Reporter!
        };

        await _dbContext.Database.ExecuteSqlRawAsync(sql, parameters);
    }
}
