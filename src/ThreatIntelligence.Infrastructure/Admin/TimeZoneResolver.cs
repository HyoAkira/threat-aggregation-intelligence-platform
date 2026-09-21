using ThreatIntelligence.Application.Abstractions.Time;

namespace ThreatIntelligence.Infrastructure.Admin;

/// <summary>
/// Resolves IANA timezone identifiers on the deployed Linux runtime and falls back to the Windows
/// id mapping when one is available, so the same accepted browser identifiers work across hosts.
/// </summary>
public sealed class TimeZoneResolver : ITimeZoneResolver
{
    public bool TryResolve(string? timeZoneId, out TimeZoneInfo timeZone)
    {
        timeZone = TimeZoneInfo.Utc;
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return false;
        }

        var id = timeZoneId.Trim();

        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(id);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            // Fall through to the IANA/Windows cross-mapping below.
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }

        try
        {
            if (TimeZoneInfo.TryConvertIanaIdToWindowsId(id, out var windowsId)
                || TimeZoneInfo.TryConvertWindowsIdToIanaId(id, out windowsId))
            {
                timeZone = TimeZoneInfo.FindSystemTimeZoneById(windowsId);
                return true;
            }
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }

        return false;
    }
}
