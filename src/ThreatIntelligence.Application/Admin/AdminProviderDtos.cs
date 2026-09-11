namespace ThreatIntelligence.Application.Admin;

/// <summary>One provider row for the Admin dashboard. Never-run providers carry null status/time fields.</summary>
public sealed record AdminProviderDashboardItem(
    int Id,
    string Name,
    string BaseUrl,
    bool IsEnabled,
    int IntervalMinutes,
    string? LastFetchStatus,
    DateTime? LastFetchAt,
    DateTime? NextFetchTime,
    IReadOnlyList<string> AvailableModes);

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
