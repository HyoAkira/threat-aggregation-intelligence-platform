using System.IO.Compression;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ThreatIntelligence.Application.Abstractions;
using ThreatIntelligence.Domain.Entities;

namespace ThreatIntelligence.Infrastructure.Services;

public class PhishTankApiService : IPhishTankApiService
{
    private readonly ThreatIntelDbContext _dbContext;
    private readonly HttpClient _httpClient;

    public PhishTankApiService(ThreatIntelDbContext dbContext, HttpClient httpClient)
    {
        _dbContext = dbContext;
        _httpClient = httpClient;
    }

    public async Task<int> DownloadDatasetAsync(CancellationToken cancellationToken = default)
    {
        var url = "online-valid.json.gz";

        using var response = await _httpClient.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException($"PhishTank API returned {(int)response.StatusCode}: {errorBody}");
        }

        var compressedStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var gzipStream = new GZipStream(compressedStream, CompressionMode.Decompress, leaveOpen: true);
        using var doc = JsonDocument.Parse(gzipStream);

        var entities = ParseJsonArray(doc);
        foreach (var entity in entities)
        {
            await UpsertPhishTankAsync(entity, cancellationToken);
        }

        return entities.Count;
    }

    private List<PhishTank> ParseJsonArray(JsonDocument doc)
    {
        var entities = new List<PhishTank>();

        if (doc.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("PhishTank API response is not a JSON array.");
        }

        foreach (var element in doc.RootElement.EnumerateArray())
        {
            var entity = MapJsonToEntity(element);
            entities.Add(entity);
        }

        if (entities.Count == 0)
        {
            throw new InvalidDataException("PhishTank API response contained no phish entries.");
        }

        return entities;
    }

    private PhishTank MapJsonToEntity(JsonElement element)
    {
        var entity = new PhishTank();

        entity.PhishId = element.TryGetProperty("phish_id", out var idProp) && idProp.ValueKind == JsonValueKind.Number
            ? idProp.GetInt32()
            : 0;

        entity.Url = element.TryGetProperty("url", out var urlProp) && urlProp.ValueKind == JsonValueKind.String
            ? urlProp.GetString() ?? string.Empty
            : string.Empty;

        entity.PhishDetailUrl = element.TryGetProperty("phish_detail_url", out var detailUrlProp) && detailUrlProp.ValueKind == JsonValueKind.String
            ? detailUrlProp.GetString() ?? string.Empty
            : string.Empty;

        entity.SubmissionTime = element.TryGetProperty("submission_time", out var submissionProp) && submissionProp.ValueKind == JsonValueKind.String && DateTime.TryParse(submissionProp.GetString(), out var submissionTime)
            ? submissionTime
            : DateTime.UtcNow;

        entity.Verified = element.TryGetProperty("verified", out var verifiedProp) && verifiedProp.ValueKind == JsonValueKind.String
            ? string.Equals(verifiedProp.GetString(), "yes", StringComparison.OrdinalIgnoreCase)
            : false;

        entity.VerificationTime = element.TryGetProperty("verification_time", out var verificationProp) && verificationProp.ValueKind == JsonValueKind.String && DateTime.TryParse(verificationProp.GetString(), out var verificationTime)
            ? verificationTime
            : null;

        entity.Online = element.TryGetProperty("online", out var onlineProp) && onlineProp.ValueKind == JsonValueKind.String
            ? string.Equals(onlineProp.GetString(), "yes", StringComparison.OrdinalIgnoreCase)
            : false;

        entity.Target = element.TryGetProperty("target", out var targetProp) && targetProp.ValueKind == JsonValueKind.String
            ? targetProp.GetString()
            : null;

        entity.Details = element.TryGetProperty("details", out var detailsProp) && detailsProp.ValueKind != JsonValueKind.Undefined
            ? detailsProp.GetRawText()
            : null;

        return entity;
    }

    private async Task UpsertPhishTankAsync(PhishTank entity, CancellationToken cancellationToken)
    {
        var sql = @"
MERGE INTO [providers].[PhishTank] AS target
USING (VALUES (@P0, @P1, @P2, @P3, @P4, @P5, @P6, @P7, @P8))
AS source (PhishId, Url, PhishDetailUrl, SubmissionTime, Verified, VerificationTime, Online, Target, Details)
ON target.PhishId = source.PhishId
WHEN MATCHED THEN
    UPDATE SET
        Url = source.Url,
        PhishDetailUrl = source.PhishDetailUrl,
        SubmissionTime = source.SubmissionTime,
        Verified = source.Verified,
        VerificationTime = source.VerificationTime,
        Online = source.Online,
        Target = source.Target,
        Details = source.Details
WHEN NOT MATCHED THEN
    INSERT (PhishId, Url, PhishDetailUrl, SubmissionTime, Verified, VerificationTime, Online, Target, Details)
    VALUES (source.PhishId, source.Url, source.PhishDetailUrl, source.SubmissionTime, source.Verified, source.VerificationTime, source.Online, source.Target, source.Details);";

        var parameters = new object?[]
        {
            entity.PhishId,
            entity.Url!,
            entity.PhishDetailUrl!,
            entity.SubmissionTime,
            entity.Verified,
            entity.VerificationTime,
            entity.Online,
            entity.Target,
            entity.Details
        };

        await _dbContext.Database.ExecuteSqlRawAsync(sql, parameters, cancellationToken);
    }
}
