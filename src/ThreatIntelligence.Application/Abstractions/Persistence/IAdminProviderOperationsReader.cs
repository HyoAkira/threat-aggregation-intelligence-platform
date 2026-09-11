using ThreatIntelligence.Application.Admin;

namespace ThreatIntelligence.Application.Abstractions.Persistence;

/// <summary>
/// Read model for the Admin provider-operations surface: the provider dashboard, one provider's
/// paginated fetch history, and a single logical job by its <c>jobId</c>. All reads are no-tracking
/// and never create history rows.
/// </summary>
public interface IAdminProviderOperationsReader
{
    Task<IReadOnlyList<AdminProviderDashboardItem>> GetProvidersAsync(CancellationToken cancellationToken);

    /// <summary>Returns null when the provider does not exist.</summary>
    Task<AdminProviderHistoryPage?> GetProviderHistoryAsync(
        int providerId,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    /// <summary>Returns null when no logical job matches the id.</summary>
    Task<AdminFetchHistoryItem?> GetFetchHistoryAsync(Guid jobId, CancellationToken cancellationToken);
}
