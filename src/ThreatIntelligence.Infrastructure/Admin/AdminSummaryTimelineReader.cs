using Microsoft.EntityFrameworkCore;
using ThreatIntelligence.Application.Abstractions.Persistence;
using ThreatIntelligence.Application.Admin;
using ThreatIntelligence.Domain.Enums;
using ThreatIntelligence.Infrastructure.Persistence;

namespace ThreatIntelligence.Infrastructure.Admin;

/// <summary>
/// Aggregates persisted <c>recordsAdded</c>/<c>recordsUpdated</c> counters from successful
/// fetch-history rows into daily buckets. Filtering stays in UTC against <c>FinishedAt</c> so SQL
/// Server can use the supporting index; timezone conversion and local-day grouping then happen in
/// memory over the bounded projection. Only completion and counter columns are projected.
/// </summary>
public sealed class AdminSummaryTimelineReader(ThreatIntelligenceDbContext dbContext)
    : IAdminSummaryTimelineReader
{
    public async Task<AdminTimeline> GetTimelineAsync(
        DateTime fromUtc,
        DateTime toUtc,
        TimeZoneInfo timeZone,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.FetchHistory.AsNoTracking()
            .Where(history => history.Status == FetchStatus.Success
                && history.FinishedAt != null
                && history.FinishedAt >= fromUtc
                && history.FinishedAt < toUtc)
            .Select(history => new
            {
                history.FinishedAt,
                history.RecordsAdded,
                history.RecordsUpdated
            })
            .ToListAsync(cancellationToken);

        var buckets = new Dictionary<DateOnly, (int Added, int Updated)>();
        foreach (var row in rows)
        {
            var finishedUtc = DateTime.SpecifyKind(row.FinishedAt!.Value, DateTimeKind.Utc);
            var localDate = TimelineCalendar.ToLocalDate(finishedUtc, timeZone);
            buckets.TryGetValue(localDate, out var current);
            buckets[localDate] = (current.Added + row.RecordsAdded, current.Updated + row.RecordsUpdated);
        }

        var dates = TimelineCalendar.EnumerateLocalDates(fromUtc, toUtc, timeZone);
        var points = new List<AdminTimelinePoint>(dates.Count);
        foreach (var date in dates)
        {
            buckets.TryGetValue(date, out var bucket);
            points.Add(new AdminTimelinePoint(
                date,
                TimelineCalendar.LocalMidnightUtc(date, timeZone),
                bucket.Added,
                bucket.Updated));
        }

        return new AdminTimeline(
            DateTime.SpecifyKind(fromUtc, DateTimeKind.Utc),
            DateTime.SpecifyKind(toUtc, DateTimeKind.Utc),
            timeZone.Id,
            rows.Count > 0,
            points);
    }
}
