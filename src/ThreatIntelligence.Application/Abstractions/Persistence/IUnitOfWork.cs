namespace ThreatIntelligence.Application.Abstractions.Persistence;

/// <summary>
/// Transaction boundary used by ingestion. Changes staged on the unit-of-work context commit
/// atomically or roll back entirely, leaving no partial writes on failure.
/// </summary>
public interface IUnitOfWork
{
    Task BeginAsync(CancellationToken cancellationToken);

    Task CommitAsync(CancellationToken cancellationToken);

    Task RollbackAsync(CancellationToken cancellationToken);
}