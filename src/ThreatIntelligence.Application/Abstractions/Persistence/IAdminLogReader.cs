using ThreatIntelligence.Application.Admin;

namespace ThreatIntelligence.Application.Abstractions.Persistence;

/// <summary>
/// Reads newest-first structured entries from the configured rolling log files only. The caller
/// supplies a bounded <paramref name="count"/> and an optional level filter; it can never supply a
/// file path.
/// </summary>
public interface IAdminLogReader
{
    Task<IReadOnlyList<AdminLogEntry>> ReadAsync(
        int count,
        string? level,
        string source,
        string? correlationId,
        CancellationToken cancellationToken);

    Task<AdminLogPage> ReadPageAsync(
        int page,
        int pageSize,
        string? level,
        string source,
        string? correlationId,
        DateTimeOffset? dateFrom,
        DateTimeOffset? dateTo,
        string orderDir,
        CancellationToken cancellationToken);
}
