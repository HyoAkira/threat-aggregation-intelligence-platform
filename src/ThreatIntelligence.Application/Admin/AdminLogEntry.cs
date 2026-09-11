namespace ThreatIntelligence.Application.Admin;

/// <summary>
/// One structured log line returned by the Admin log tail. <see cref="Exception"/> is optional and,
/// like <see cref="Message"/>, is redacted before it leaves the API.
/// </summary>
public sealed record AdminLogEntry(
    DateTimeOffset Timestamp,
    string Level,
    string Message,
    string? Exception);
