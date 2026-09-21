namespace ThreatIntelligence.Application.Abstractions.Time;

/// <summary>
/// Resolves browser-supplied IANA timezone identifiers to a runtime <see cref="TimeZoneInfo"/>.
/// Validation happens server-side in one place so the deployed Linux runtime and a UTC fallback
/// are handled consistently across the Admin timeline surface.
/// </summary>
public interface ITimeZoneResolver
{
    bool TryResolve(string? timeZoneId, out TimeZoneInfo timeZone);
}
