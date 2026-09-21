using Microsoft.EntityFrameworkCore;
using ThreatIntelligence.Application.Abstractions.Persistence;
using ThreatIntelligence.Domain.Entities;
using ThreatIntelligence.Infrastructure.Persistence;

namespace ThreatIntelligence.Infrastructure.Persistence.Repositories;

/// <summary>
/// Uses a fresh DbContext per operation so history writes are never inside the IOC transaction.
/// </summary>
public sealed class FetchHistoryRepository(
    IDbContextFactory<ThreatIntelligenceDbContext> contextFactory) : IFetchHistoryRepository
{
    public async Task AddAsync(FetchHistory history, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        context.FetchHistory.Add(history);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(FetchHistory history, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        context.FetchHistory.Update(history);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<FetchHistory?> GetByJobIdAsync(Guid jobId, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.FetchHistory
            .AsNoTracking()
            .SingleOrDefaultAsync(h => h.JobId == jobId, cancellationToken);
    }
}