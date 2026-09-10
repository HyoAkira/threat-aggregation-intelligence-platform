using Microsoft.EntityFrameworkCore;
using ThreatIntelligence.Application.Fetches;
using ThreatIntelligence.Domain.Enums;
using ThreatIntelligence.Infrastructure.Fetches;

namespace ThreatIntelligence.IntegrationTests;

[Collection("sqlserver")]
public sealed class FetchGuardTests(SqlServerFixture fixture) : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await fixture.ResetDataAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Concurrent_reservations_create_only_one_queued_history_row()
    {
        var guard = new FetchGuard(fixture.CreateContextFactory(), TimeProvider.System);

        var reservations = await Task.WhenAll(
            guard.ReserveAsync(1, FetchTrigger.Manual, FetchMode.Recent, CancellationToken.None),
            guard.ReserveAsync(1, FetchTrigger.Scheduled, FetchMode.Recent, CancellationToken.None));

        Assert.Single(reservations, reservation => reservation.IsAllowed);
        Assert.Single(reservations, reservation => reservation.Rejection == FetchReservationRejection.ActiveFetch);

        await using var context = fixture.CreateContext();
        Assert.Equal(1, await context.FetchHistory.CountAsync());
        Assert.Equal(FetchStatus.Queued, await context.FetchHistory.Select(history => history.Status).SingleAsync());
    }

    [Fact]
    public async Task Recent_cooldown_rejects_a_new_reservation_and_terminal_duplicate_delivery()
    {
        var guard = new FetchGuard(fixture.CreateContextFactory(), TimeProvider.System);
        var lifecycle = new FetchLifecycle(fixture.CreateContextFactory(), TimeProvider.System);
        var reservation = await guard.ReserveAsync(1, FetchTrigger.Manual, FetchMode.Recent, CancellationToken.None);
        var job = new FetchJob(reservation.JobId!.Value, 1, FetchTrigger.Manual, FetchMode.Recent, 1);

        Assert.True(await guard.CanExecuteAsync(job, CancellationToken.None));
        await lifecycle.FailAsync(job.JobId, "publish failed", CancellationToken.None);

        var blocked = await guard.ReserveAsync(1, FetchTrigger.Manual, FetchMode.Recent, CancellationToken.None);
        Assert.Equal(FetchReservationRejection.Cooldown, blocked.Rejection);
        Assert.False(await guard.CanExecuteAsync(job, CancellationToken.None));
    }
}
