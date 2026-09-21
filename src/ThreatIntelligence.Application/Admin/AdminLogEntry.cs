namespace ThreatIntelligence.Application.Admin;

/// <summary>
/// One structured log line returned by the Admin log tail. <see cref="CorrelationId"/>, like
/// <see cref="Message"/> and <see cref="Exception"/>, is optional and is redacted before it leaves
/// the API.
/// </summary>
public sealed record AdminLogEntry(
    DateTimeOffset Timestamp,
    string Level,
    string? CorrelationId,
    string Message,
    string? Exception);

/// <summary>One server-side page of structured admin log entries.</summary>
public sealed record AdminLogPage(
    IReadOnlyList<AdminLogEntry> Items,
    int Page,
    int PageSize,
    int TotalCount);
