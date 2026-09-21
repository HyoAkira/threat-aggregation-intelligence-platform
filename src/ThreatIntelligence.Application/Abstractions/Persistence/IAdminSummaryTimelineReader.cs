using ThreatIntelligence.Application.Admin;

namespace ThreatIntelligence.Application.Abstractions.Persistence;

/// <summary>
/// Read model for the bounded Admin IOC activity timeline. Aggregates persisted
/// <c>recordsAdded</c>/<c>recordsUpdated</c> counters from successful fetch-history rows into daily
/// buckets in the requesting browser's timezone. Kept separate from
/// <see cref="IAdminSummaryReader"/> so the established summary contract and query are untouched.
/// </summary>
public interface IAdminSummaryTimelineReader
{
    Task<AdminTimeline> GetTimelineAsync(
        DateTime fromUtc,
        DateTime toUtc,
        TimeZoneInfo timeZone,
        CancellationToken cancellationToken);
}
