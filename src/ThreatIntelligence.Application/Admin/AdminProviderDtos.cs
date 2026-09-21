namespace ThreatIntelligence.Application.Admin;

/// <summary>One provider row for the Admin dashboard. Never-run providers carry null status/time fields.
/// The configuration fields mirror <see cref="ProviderWriteRequest"/> so a read item can be edited and
/// submitted back without translation.</summary>
public sealed record AdminProviderDashboardItem(
    int Id,
    string Name,
    string BaseUrl,
    bool IsEnabled,
    int IntervalMinutes,
    string? LastFetchStatus,
    DateTime? LastFetchAt,
    DateTime? NextFetchTime,
    IReadOnlyList<string> SupportedModes,
    string DefaultMode,
    int? RecentCooldownMinutes,
    int? FullCooldownMinutes,
    int MinimumIntervalMinutes,
    bool RequiresApiKey = false,
    bool CredentialConfigured = false,
    int? CredentialKeyVersion = null,
    DateTime? CredentialUpdatedAt = null);

/// <summary>One logical fetch-history row. <see cref="ErrorDetail"/> is the safe terminal error only.</summary>
public sealed record AdminFetchHistoryItem(
    Guid JobId,
    int ProviderId,
    string Status,
    string Trigger,
    string Mode,
    DateTime StartedAt,
    DateTime? FinishedAt,
    int RecordsAdded,
    int RecordsUpdated,
    string? ErrorDetail);

/// <summary>Shared page envelope for Admin history; mirrors the E09 paged contract.</summary>
public sealed record AdminProviderHistoryPage(
    IReadOnlyList<AdminFetchHistoryItem> Items,
    int Page,
    int PageSize,
    int TotalCount);

/// <summary>Distinct normalized IOC group counts by type.</summary>
public sealed record AdminSummary(int Total, int Ipv4, int Ipv6, int Domain, int Url);

/// <summary>Admin create/update payload for a provider. Modes use the lowercase read-model tokens.</summary>
public sealed record ProviderWriteRequest(
    string Name,
    string BaseUrl,
    int IntervalMinutes,
    bool IsEnabled,
    string DefaultMode,
    IReadOnlyList<string> SupportedModes,
    int? RecentCooldownMinutes,
    int? FullCooldownMinutes,
    int MinimumIntervalMinutes,
    bool RequiresApiKey = false);

public enum ProviderWriteStatus
{
    Success,
    Invalid,
    DuplicateName,
    NotFound,
    InUse
}

public sealed record ProviderWriteResult(ProviderWriteStatus Status, AdminProviderDashboardItem? Provider);
