using Microsoft.EntityFrameworkCore.Storage;
using ThreatIntelligence.Application.Abstractions.Persistence;
using ThreatIntelligence.Infrastructure.Persistence;

namespace ThreatIntelligence.Infrastructure.Persistence.Repositories;

public sealed class UnitOfWork(ThreatIntelligenceDbContext dbContext) : IUnitOfWork
{
    private IDbContextTransaction? _transaction;

    public async Task BeginAsync(CancellationToken cancellationToken)
    {
        _transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        await dbContext.SaveChangesAsync(cancellationToken);

        if (_transaction is not null)
        {
            await _transaction.CommitAsync(cancellationToken);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async Task RollbackAsync(CancellationToken cancellationToken)
    {
        if (_transaction is not null)
        {
            await _transaction.RollbackAsync(cancellationToken);
            await _transaction.DisposeAsync();
            _transaction = null;
        }

        dbContext.ChangeTracker.Clear();
    }
}