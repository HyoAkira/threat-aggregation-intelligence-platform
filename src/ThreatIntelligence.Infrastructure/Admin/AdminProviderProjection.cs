using ThreatIntelligence.Application.Admin;
using ThreatIntelligence.Application.Fetches;
using ThreatIntelligence.Domain.Entities;
using ThreatIntelligence.Domain.Enums;

namespace ThreatIntelligence.Infrastructure.Admin;

/// <summary>
/// Single projection of a provider row into the admin dashboard contract, shared by the read model
/// and the write model so advertised modes and default mode cannot drift between them.
/// </summary>
internal static class AdminProviderProjection
{
    public static AdminProviderDashboardItem Project(Provider provider) => new(
        provider.Id,
        provider.Name,
        provider.BaseUrl,
        provider.IsEnabled,
        provider.IntervalMinutes,
        provider.LastFetchStatus is { } status ? FetchTokens.Status(status) : null,
        provider.LastFetchAt,
        provider.NextFetchTime,
        SupportedModes(provider),
        FetchTokens.Mode(provider.DefaultMode),
        provider.RecentCooldownMinutes,
        provider.FullCooldownMinutes,
        provider.MinimumIntervalMinutes);

    private static IReadOnlyList<string> SupportedModes(Provider provider) =>
        Enum.GetValues<FetchMode>()
            .Where(provider.HasMode)
            .Select(FetchTokens.Mode)
            .ToList();
}
