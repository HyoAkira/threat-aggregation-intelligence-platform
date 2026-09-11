using Microsoft.EntityFrameworkCore;
using ThreatIntelligence.Application.Fetches;
using ThreatIntelligence.Domain.Entities;
using ThreatIntelligence.Domain.Enums;
using ThreatIntelligence.Infrastructure.Persistence;

namespace ThreatIntelligence.Infrastructure.Fetches;

/// <summary>
/// Shared guard math used by both trigger-time reservation and consume-time rechecks so the two
/// paths cannot diverge. The current job is excluded from the active/cooldown lookups.
/// </summary>
public static class FetchGuardEvaluator
{
    public static async Task<FetchReservationRejection?> GetRejectionAsync(
        ThreatIntelligenceDbContext context,
        Provider? provider,
        Guid? currentJobId,
        FetchMode mode,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        if (provider is null)
        {
            return FetchReservationRejection.ProviderNotFound;
        }

        if (!Supports(provider, mode))
        {
            return FetchReservationRejection.UnsupportedMode;
        }

        if (!provider.IsEnabled)
        {
            return FetchReservationRejection.Disabled;
        }

        if (await context.FetchHistory.AnyAsync(
                h => h.ProviderId == provider.Id && h.JobId != currentJobId &&
                    (h.Status == FetchStatus.Queued || h.Status == FetchStatus.Running), cancellationToken))
        {
            return FetchReservationRejection.ActiveFetch;
        }

        var cooldown = Cooldown(provider, mode);
        if (cooldown is null)
        {
            return null;
        }

        var latest = await context.FetchHistory
            .Where(h => h.ProviderId == provider.Id && h.Mode == mode && h.JobId != currentJobId)
            .OrderByDescending(h => h.StartedAt)
            .Select(h => (DateTime?)h.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);
        return latest.HasValue && nowUtc - latest.Value < cooldown.Value
            ? FetchReservationRejection.Cooldown
            : null;
    }

    public static bool Supports(Provider provider, FetchMode mode) => provider.Name switch
    {
        "ThreatFox" => mode is FetchMode.Recent or FetchMode.Full,
        "PhishTank" => mode == FetchMode.Full,
        _ => false
    };

    private static TimeSpan? Cooldown(Provider provider, FetchMode mode) =>
        provider.Name == "ThreatFox"
            ? mode == FetchMode.Recent ? TimeSpan.FromMinutes(5) : TimeSpan.FromMinutes(60)
            : null;
}
