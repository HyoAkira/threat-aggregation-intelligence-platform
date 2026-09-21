using System.Globalization;
using ThreatIntelligence.Application.Abstractions.Time;

namespace ThreatIntelligence.Application.Admin;

/// <summary>Outcome of validating an Admin timeline request. When <see cref="IsValid"/> is false
/// the error fields carry the safe ProblemDetails code/detail the controller should return.</summary>
public sealed record TimelineRequestValidation(
    bool IsValid,
    DateTime FromUtc,
    DateTime ToUtc,
    TimeZoneInfo? TimeZone,
    string? ErrorCode,
    string? ErrorDetail);

/// <summary>
/// Validates the bounded UTC range and IANA timezone supplied to the Admin timeline endpoint
/// before any history query runs. The range is half-open <c>[from,to)</c> and capped at
/// <see cref="TimelineCalendar.MaxLocalDays"/> local calendar days in the requested timezone.
/// </summary>
public static class TimelineRequestValidator
{
    public const string InvalidRangeCode = "invalid_timeline_range";
    public const string InvalidTimeZoneCode = "invalid_timeline_timezone";
    public const string RangeTooLargeCode = "timeline_range_too_large";

    public static TimelineRequestValidation Validate(
        string? from,
        string? to,
        string? timeZoneId,
        ITimeZoneResolver timeZoneResolver)
    {
        if (!TryParseUtcInstant(from, out var fromUtc) || !TryParseUtcInstant(to, out var toUtc))
        {
            return Invalid(
                InvalidRangeCode,
                "The from and to values must be ISO-8601 UTC instants, for example 2026-09-14T00:00:00Z.");
        }

        if (!timeZoneResolver.TryResolve(timeZoneId, out var timeZone))
        {
            return Invalid(
                InvalidTimeZoneCode,
                "The timeZone value is not a recognized IANA timezone identifier.");
        }

        if (fromUtc >= toUtc)
        {
            return Invalid(
                InvalidRangeCode,
                "The from instant must be earlier than the to instant.");
        }

        if (TimelineCalendar.EnumerateLocalDates(fromUtc, toUtc, timeZone).Count > TimelineCalendar.MaxLocalDays)
        {
            return Invalid(
                RangeTooLargeCode,
                $"The requested range must not exceed {TimelineCalendar.MaxLocalDays} local calendar days.");
        }

        return new TimelineRequestValidation(true, fromUtc, toUtc, timeZone, null, null);
    }

    /// <summary>
    /// Parses an explicit UTC ISO-8601 instant. Offset-less values are rejected so a local-time
    /// string is never silently interpreted as UTC.
    /// </summary>
    private static bool TryParseUtcInstant(string? value, out DateTime result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        var hasUtcDesignator =
            trimmed.EndsWith("Z", StringComparison.OrdinalIgnoreCase)
            || trimmed.EndsWith("+00:00", StringComparison.Ordinal)
            || trimmed.EndsWith("+0000", StringComparison.Ordinal);

        if (!hasUtcDesignator
            || !DateTimeOffset.TryParse(
                trimmed,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var parsed)
            || parsed.Offset != TimeSpan.Zero)
        {
            return false;
        }

        result = parsed.UtcDateTime;
        return true;
    }

    private static TimelineRequestValidation Invalid(string code, string detail) =>
        new(false, default, default, null, code, detail);
}
