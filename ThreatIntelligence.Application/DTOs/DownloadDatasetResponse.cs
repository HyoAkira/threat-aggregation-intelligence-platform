namespace ThreatIntelligence.Application.DTOs;

public record DownloadDatasetResponse(int RecordsProcessed, string Message, DateTime FetchedAtUtc);