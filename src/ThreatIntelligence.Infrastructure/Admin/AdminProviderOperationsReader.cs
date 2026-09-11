using Microsoft.EntityFrameworkCore;
using ThreatIntelligence.Application.Abstractions.Persistence;
using ThreatIntelligence.Application.Admin;
using ThreatIntelligence.Application.Fetches;
using ThreatIntelligence.Domain.Entities;
using ThreatIntelligence.Domain.Enums;
using ThreatIntelligence.Infrastructure.Fetches;
using ThreatIntelligence.Infrastructure.Persistence;

namespace ThreatIntelligence.Infrastructure.Admin;

/// <summary>
/// EF Core read model for Admin provider operations. All reads are no-tracking and never create or
/// mutate history rows; secrets (API keys, connection strings) are never projected.
/// </summary>
public sealed class AdminProviderOperationsReader(IDbContextFactory<ThreatIntelligenceDbContext> contextFactory)
    : IAdminProviderOperationsReader
{
    public async Task<IReadOnlyList<AdminProviderDashboardItem>> GetProvidersAsync(CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var providers = await context.Providers.AsNoTracking()
            .OrderBy(provider => provider.Id)
            .ToListAsync(cancellationToken);

        return providers.Select(provider => new AdminProviderDashboardItem(
            provider.Id,
            provider.Name,
            provider.BaseUrl,
            provider.IsEnabled,
            provider.IntervalMinutes,
            provider.LastFetchStatus is { } status ? FetchTokens.Status(status) : null,
            provider.LastFetchAt,
            provider.NextFetchTime,
            AvailableModes(provider))).ToList();
    }

    public async Task<AdminProviderHistoryPage?> GetProviderHistoryAsync(
        int providerId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        if (!await context.Providers.AsNoTracking().AnyAsync(provider => provider.Id == providerId, cancellationToken))
        {
            return null;
        }

        var query = context.FetchHistory.AsNoTracking().Where(history => history.ProviderId == providerId);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(history => history.StartedAt)
            .ThenByDescending(history => history.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new AdminProviderHistoryPage(items.Select(Map).ToList(), page, pageSize, totalCount);
    }

    public async Task<AdminFetchHistoryItem?> GetFetchHistoryAsync(Guid jobId, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var history = await context.FetchHistory.AsNoTracking()
            .SingleOrDefaultAsync(record => record.JobId == jobId, cancellationToken);
        return history is null ? null : Map(history);
    }

    private static AdminFetchHistoryItem Map(FetchHistory history) => new(
        history.JobId,
        history.ProviderId,
        FetchTokens.Status(history.Status),
        FetchTokens.Trigger(history.Trigger),
        FetchTokens.Mode(history.Mode),
        history.StartedAt,
        history.FinishedAt,
        history.RecordsAdded,
        history.RecordsUpdated,
        history.ErrorDetail);

    private static IReadOnlyList<string> AvailableModes(Provider provider) =>
        Enum.GetValues<FetchMode>()
            .Where(mode => FetchGuardEvaluator.Supports(provider, mode))
            .Select(FetchTokens.Mode)
            .ToList();
}
