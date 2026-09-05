namespace ThreatIntelligence.Application.DTOs;

public record GetRecentIoCsResponse(int RecordsProcessed, string Message, DateTime FetchedAtUtc);