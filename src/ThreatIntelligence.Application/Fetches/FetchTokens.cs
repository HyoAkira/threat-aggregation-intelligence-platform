using ThreatIntelligence.Domain.Enums;

namespace ThreatIntelligence.Application.Fetches;

/// <summary>
/// Stable lowercase tokens for fetch status, trigger, and mode. Shared by the Admin read model and
/// the structured lifecycle events so both surfaces use one vocabulary.
/// </summary>
public static class FetchTokens
{
    public static string Status(FetchStatus status) => status switch
    {
        FetchStatus.Queued => "queued",
        FetchStatus.Running => "running",
        FetchStatus.Success => "success",
        FetchStatus.Failed => "failed",
        _ => status.ToString().ToLowerInvariant()
    };

    public static string Trigger(FetchTrigger trigger) => trigger switch
    {
        FetchTrigger.Scheduled => "scheduled",
        FetchTrigger.Manual => "manual",
        _ => trigger.ToString().ToLowerInvariant()
    };

    public static string Mode(FetchMode mode) => mode switch
    {
        FetchMode.Recent => "recent",
        FetchMode.Full => "full",
        _ => mode.ToString().ToLowerInvariant()
    };

    /// <summary>Parses a lowercase mode token; returns null for an unknown or blank token.</summary>
    public static FetchMode? ParseMode(string? token) =>
        Enum.TryParse<FetchMode>(token, ignoreCase: true, out var mode) && Enum.IsDefined(mode)
            ? mode
            : null;
}
