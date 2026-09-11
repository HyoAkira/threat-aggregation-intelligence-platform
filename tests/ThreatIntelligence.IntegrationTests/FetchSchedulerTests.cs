using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ThreatIntelligence.Application.Fetches;
using ThreatIntelligence.Domain.Enums;
using ThreatIntelligence.Infrastructure.Fetches;
using ThreatIntelligence.Infrastructure.Persistence;

namespace ThreatIntelligence.IntegrationTests;

[Collection("sqlserver")]
public sealed class FetchSchedulerTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private readonly MutableTimeProvider _clock = new();

    public async Task InitializeAsync()
    {
        await fixture.ResetDataAsync();
        await using var context = fixture.CreateContext();
        await context.Database.ExecuteSqlRawAsync(
            "UPDATE providers SET nextFetchTime = NULL, lastFetchAt = NULL, lastFetchStatus = NULL, isEnabled = 1, intervalMinutes = CASE WHEN id = 1 THEN 5 ELSE 480 END;");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Never_run_provider_is_not_scheduled()
    {
        var (scheduler, publisher) = CreateScheduler();

        Assert.Equal(0, await scheduler.TickAsync(CancellationToken.None));
        Assert.Empty(publisher.Jobs);
    }

    [Fact]
    public async Task Due_provider_is_published_and_next_fetch_time_advances()
    {
        var now = _clock.UtcNow;
        await SetProviderAsync(1, nextFetchTime: now.AddMinutes(-1));
        var (scheduler, publisher) = CreateScheduler();

        Assert.Equal(1, await scheduler.TickAsync(CancellationToken.None));
        Assert.Single(publisher.Jobs);
        Assert.Equal(FetchTrigger.Scheduled, publisher.Jobs[0].Trigger);
        Assert.Equal(FetchMode.Recent, publisher.Jobs[0].Mode);

        await using var context = fixture.CreateContext();
        var provider = await context.Providers.SingleAsync(item => item.Id == 1);
        Assert.Equal(now.AddMinutes(5), provider.NextFetchTime);
    }

    [Fact]
    public async Task Disabled_provider_is_not_scheduled()
    {
        var now = _clock.UtcNow;
        await SetProviderAsync(1, nextFetchTime: now.AddMinutes(-1), isEnabled: false);
        var (scheduler, publisher) = CreateScheduler();

        Assert.Equal(0, await scheduler.TickAsync(CancellationToken.None));
        Assert.Empty(publisher.Jobs);
    }

    [Fact]
    public async Task Duplicate_tick_does_not_double_publish()
    {
        var now = _clock.UtcNow;
        await SetProviderAsync(1, nextFetchTime: now.AddMinutes(-1));
        var (scheduler, publisher) = CreateScheduler();

        Assert.Equal(1, await scheduler.TickAsync(CancellationToken.None));
        Assert.Equal(0, await scheduler.TickAsync(CancellationToken.None));
        Assert.Single(publisher.Jobs);
    }

    [Fact]
    public async Task Publish_failure_marks_history_failed_and_still_advances_schedule()
    {
        var now = _clock.UtcNow;
        await SetProviderAsync(1, nextFetchTime: now.AddMinutes(-1));
        var (scheduler, _) = CreateScheduler(failPublish: true);

        Assert.Equal(0, await scheduler.TickAsync(CancellationToken.None));

        await using var context = fixture.CreateContext();
        var provider = await context.Providers.SingleAsync(item => item.Id == 1);
        Assert.Equal(now.AddMinutes(5), provider.NextFetchTime);
        Assert.Equal(FetchStatus.Failed, await context.FetchHistory.Select(history => history.Status).SingleAsync());
    }

    [Fact]
    public async Task Failed_fetch_does_not_reschedule_before_the_interval()
    {
        var now = _clock.UtcNow;
        await SetProviderAsync(1, nextFetchTime: now.AddMinutes(-1));
        var (scheduler, publisher) = CreateScheduler();

        Assert.Equal(1, await scheduler.TickAsync(CancellationToken.None));

        var lifecycle = new FetchLifecycle(fixture.CreateContextFactory(), _clock);
        await lifecycle.FailAsync(publisher.Jobs[0], "upstream failure", CancellationToken.None);

        Assert.Equal(0, await scheduler.TickAsync(CancellationToken.None));
        Assert.Single(publisher.Jobs);
    }

    private (FetchSchedulerService Scheduler, RecordingPublisher Publisher) CreateScheduler(bool failPublish = false)
    {
        var publisher = new RecordingPublisher(failPublish);
        var services = new ServiceCollection();
        services.AddSingleton<IDbContextFactory<ThreatIntelligenceDbContext>>(fixture.CreateContextFactory());
        services.AddSingleton<TimeProvider>(_clock);
        services.AddScoped<IFetchGuard, FetchGuard>();
        services.AddScoped<IFetchLifecycle, FetchLifecycle>();
        services.AddSingleton<IFetchJobPublisher>(publisher);
        var provider = services.BuildServiceProvider();
        var scheduler = new FetchSchedulerService(
            fixture.CreateContextFactory(), provider.GetRequiredService<IServiceScopeFactory>(), _clock);
        return (scheduler, publisher);
    }

    private async Task SetProviderAsync(int providerId, DateTime nextFetchTime, bool isEnabled = true)
    {
        await using var context = fixture.CreateContext();
        var provider = await context.Providers.SingleAsync(item => item.Id == providerId);
        provider.SetEnabled(isEnabled);
        provider.SetNextFetchTime(nextFetchTime);
        await context.SaveChangesAsync();
    }

    private sealed class MutableTimeProvider : TimeProvider
    {
        public DateTime UtcNow { get; } = new(2026, 9, 10, 12, 0, 0, DateTimeKind.Utc);

        public override DateTimeOffset GetUtcNow() => new(UtcNow, TimeSpan.Zero);
    }

    private sealed class RecordingPublisher(bool fail) : IFetchJobPublisher
    {
        public List<FetchJob> Jobs { get; } = [];

        public Task PublishAsync(FetchJob job, CancellationToken cancellationToken)
        {
            if (fail)
            {
                throw new InvalidOperationException("broker unavailable");
            }

            Jobs.Add(job);
            return Task.CompletedTask;
        }
    }
}
