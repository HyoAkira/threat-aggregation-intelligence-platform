namespace ThreatIntelligence.Application.Admin;

/// <summary>
/// Pure calendar arithmetic for the Admin timeline. Persisted instants stay UTC; only the
/// local-calendar projection and bucket starts cross into the requested timezone. DST correctness
/// comes from <see cref="TimeZoneInfo"/> conversion rather than fixed 24-hour offsets.
/// </summary>
public static class TimelineCalendar
{
    /// <summary>Maximum number of local calendar days a single timeline request may cover.</summary>
    public const int MaxLocalDays = 31;

    /// <summary>
    /// Local calendar dates touched by the half-open UTC interval <c>[fromUtc,toUtc)</c>. Because the
    /// interval is half-open, a <c>toUtc</c> that lands exactly on local midnight does not include
    /// that final date. Results are chronologically ascending and never empty for a valid range.
    /// </summary>
    public static IReadOnlyList<DateOnly> EnumerateLocalDates(
        DateTime fromUtc,
        DateTime toUtc,
        TimeZoneInfo timeZone)
    {
        var startDate = ToLocalDate(fromUtc, timeZone);
        var endInstantLocal = TimeZoneInfo.ConvertTimeFromUtc(toUtc, timeZone);
        var endDate = DateOnly.FromDateTime(endInstantLocal);
        if (endInstantLocal.TimeOfDay == TimeSpan.Zero)
        {
            endDate = endDate.AddDays(-1);
        }

        var dates = new List<DateOnly>();
        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            dates.Add(date);
        }

        return dates;
    }

    /// <summary>Canonical UTC instant of local midnight for <paramref name="localDate"/>.</summary>
    public static DateTime LocalMidnightUtc(DateOnly localDate, TimeZoneInfo timeZone)
    {
        var local = DateTime.SpecifyKind(localDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(local, timeZone);
    }

    /// <summary>Local calendar date of a UTC instant in the requested timezone.</summary>
    public static DateOnly ToLocalDate(DateTime utcInstant, TimeZoneInfo timeZone)
    {
        var utc = utcInstant.Kind == DateTimeKind.Utc
            ? utcInstant
            : DateTime.SpecifyKind(utcInstant, DateTimeKind.Utc);
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utc, timeZone));
    }
}
