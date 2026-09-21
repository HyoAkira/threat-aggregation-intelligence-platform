namespace ThreatIntelligence.Application.Admin;

/// <summary>One daily bucket of IOC activity. <see cref="LocalDate"/> is the calendar date in the
/// requested timezone; <see cref="BucketStartUtc"/> is that local midnight expressed as a canonical
/// UTC instant so DST transitions are preserved.</summary>
public sealed record AdminTimelinePoint(
    DateOnly LocalDate,
    DateTime BucketStartUtc,
    int RecordsAdded,
    int RecordsUpdated);

/// <summary>Bounded timezone-aware IOC activity timeline. <see cref="HasSuccessfulFetches"/> is
/// false when no successful fetch completed inside the half-open <c>[From,To)</c> interval, which
/// is distinct from a successful fetch that contributed zero counters.</summary>
public sealed record AdminTimeline(
    DateTime From,
    DateTime To,
    string TimeZone,
    bool HasSuccessfulFetches,
    IReadOnlyList<AdminTimelinePoint> Points);
