using Microsoft.EntityFrameworkCore;
using ThreatIntelligence.Application.Fetches;
using ThreatIntelligence.Domain.Enums;
using ThreatIntelligence.Infrastructure.Fetches;

namespace ThreatIntelligence.IntegrationTests;

[Collection("sqlserver")]
public sealed class FetchLifecycleTests(SqlServerFixture fixture) : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await fixture.ResetDataAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Repeated_start_reuses_one_history_row_and_success_persists_counters()
    {
        var guard = new FetchGuard(fixture.CreateContextFactory(), TimeProvider.System);
        var lifecycle = new FetchLifecycle(fixture.CreateContextFactory(), TimeProvider.System);
        var reservation = await guard.ReserveAsync(1, FetchTrigger.Manual, FetchMode.Recent, CancellationToken.None);
        var job = new FetchJob(reservation.JobId!.Value, 1, FetchTrigger.Manual, FetchMode.Recent, 1);

        Assert.True(await lifecycle.TryStartAsync(job, false, CancellationToken.None));
        Assert.True(await lifecycle.TryStartAsync(job, false, CancellationToken.None));
        await lifecycle.SucceedAsync(job, 2, 3, CancellationToken.None);

        await using var context = fixture.CreateContext();
        var history = await context.FetchHistory.SingleAsync();
        var provider = await context.Providers.SingleAsync(provider => provider.Id == 1);
        Assert.Equal(job.JobId, history.JobId);
        Assert.Equal(FetchStatus.Success, history.Status);
        Assert.Equal(2, history.RecordsAdded);
        Assert.Equal(3, history.RecordsUpdated);
        Assert.Equal(FetchStatus.Success, provider.LastFetchStatus);
        Assert.NotNull(provider.LastFetchAt);
        Assert.NotNull(provider.NextFetchTime);
    }

    [Fact]
    public async Task Terminal_failure_preserves_one_history_row_with_zero_counters()
    {
        var guard = new FetchGuard(fixture.CreateContextFactory(), TimeProvider.System);
        var lifecycle = new FetchLifecycle(fixture.CreateContextFactory(), TimeProvider.System);
        var reservation = await guard.ReserveAsync(1, FetchTrigger.Manual, FetchMode.Recent, CancellationToken.None);
        var job = new FetchJob(reservation.JobId!.Value, 1, FetchTrigger.Manual, FetchMode.Recent, 1);

        Assert.True(await lifecycle.TryStartAsync(job, false, CancellationToken.None));
        await lifecycle.FailAsync(job, "invalid provider record", CancellationToken.None);

        await using var context = fixture.CreateContext();
        var history = await context.FetchHistory.SingleAsync();
        var provider = await context.Providers.SingleAsync(provider => provider.Id == 1);
        Assert.Equal(FetchStatus.Failed, history.Status);
        Assert.Equal(0, history.RecordsAdded);
        Assert.Equal(0, history.RecordsUpdated);
        Assert.Equal("invalid provider record", history.ErrorDetail);
        Assert.Equal(FetchStatus.Failed, provider.LastFetchStatus);
    }
}
