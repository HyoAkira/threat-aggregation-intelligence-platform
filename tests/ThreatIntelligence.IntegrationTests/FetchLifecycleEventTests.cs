using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThreatIntelligence.API.Controllers;
using ThreatIntelligence.Application.Fetches;
using ThreatIntelligence.Domain.Enums;
using ThreatIntelligence.Infrastructure.Fetches;

namespace ThreatIntelligence.IntegrationTests;

/// <summary>
/// TI-106: reservation, guard rejection, execution start, retry, success, and terminal failure
/// events share one correlation id and job id across the API trigger and the Worker lifecycle, and
/// never expose credentials.
/// </summary>
[Collection("sqlserver")]
public sealed class FetchLifecycleEventTests(SqlServerFixture fixture) : IAsyncLifetime
{
    public async Task InitializeAsync() => await fixture.ResetDataAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task One_job_is_traceable_across_api_and_worker()
    {
        var sink = new CapturingSink();
        var publisher = new CapturingPublisher();
        var lifecycle = new FetchLifecycle(fixture.CreateContextFactory(), TimeProvider.System, sink);
        var controller = new AdminProviderFetchController(
            new FetchGuard(fixture.CreateContextFactory(), TimeProvider.System),
            publisher,
            lifecycle,
            sink)
        {
            ControllerContext = new ControllerContext { HttpContext = CreateHttpContext("corr-ti-106") }
        };

        var accepted = await controller.Fetch(1, CancellationToken.None);
        Assert.IsType<AcceptedResult>(accepted.Result);
        var job = publisher.Job!;
        Assert.Equal("corr-ti-106", job.CorrelationId);

        Assert.True(await lifecycle.TryStartAsync(job, isRetry: false, CancellationToken.None));
        await lifecycle.SucceedAsync(job, 2, 3, CancellationToken.None);

        Assert.Equal(
            [FetchLifecycleEventNames.Reservation, FetchLifecycleEventNames.ExecutionStart, FetchLifecycleEventNames.Success],
            sink.Events.Select(lifecycleEvent => lifecycleEvent.EventName));
        Assert.All(sink.Events, lifecycleEvent =>
        {
            Assert.Equal(job.JobId, lifecycleEvent.JobId);
            Assert.Equal("corr-ti-106", lifecycleEvent.CorrelationId);
            Assert.Equal(1, lifecycleEvent.ProviderId);
            Assert.Equal("recent", lifecycleEvent.Mode);
            Assert.Equal("manual", lifecycleEvent.Trigger);
        });

        var success = sink.Events.Single(lifecycleEvent => lifecycleEvent.EventName == FetchLifecycleEventNames.Success);
        Assert.NotNull(success.DurationMs);
        Assert.Equal(2, success.RecordsAdded);
        Assert.Equal(3, success.RecordsUpdated);
    }

    [Fact]
    public async Task Retry_reuses_the_job_and_emits_a_retry_event()
    {
        var sink = new CapturingSink();
        var lifecycle = new FetchLifecycle(fixture.CreateContextFactory(), TimeProvider.System, sink);
        var guard = new FetchGuard(fixture.CreateContextFactory(), TimeProvider.System);
        var reservation = await guard.ReserveAsync(1, FetchTrigger.Manual, FetchMode.Recent, CancellationToken.None);
        var job = new FetchJob(reservation.JobId!.Value, 1, FetchTrigger.Manual, FetchMode.Recent, 2, "corr-retry");

        Assert.True(await lifecycle.TryStartAsync(job, isRetry: true, CancellationToken.None));

        var retry = Assert.Single(sink.Events, lifecycleEvent => lifecycleEvent.EventName == FetchLifecycleEventNames.Retry);
        Assert.Equal(job.JobId, retry.JobId);
        Assert.Equal("corr-retry", retry.CorrelationId);
    }

    [Fact]
    public async Task Terminal_failure_reason_is_redacted()
    {
        var sink = new CapturingSink();
        var lifecycle = new FetchLifecycle(fixture.CreateContextFactory(), TimeProvider.System, sink);
        var guard = new FetchGuard(fixture.CreateContextFactory(), TimeProvider.System);
        var reservation = await guard.ReserveAsync(1, FetchTrigger.Manual, FetchMode.Recent, CancellationToken.None);
        var job = new FetchJob(reservation.JobId!.Value, 1, FetchTrigger.Manual, FetchMode.Recent, 1, "corr-fail");
        await lifecycle.TryStartAsync(job, isRetry: false, CancellationToken.None);

        await lifecycle.FailAsync(job, "upstream https://user:leaked-pass@host/feed failed", CancellationToken.None);

        var failure = Assert.Single(
            sink.Events,
            lifecycleEvent => lifecycleEvent.EventName == FetchLifecycleEventNames.TerminalFailure);
        Assert.NotNull(failure.Reason);
        Assert.DoesNotContain("leaked-pass", failure.Reason!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Guard_rejection_is_emitted_for_a_disabled_provider()
    {
        await using (var context = fixture.CreateContext())
        {
            var provider = await context.Providers.SingleAsync(item => item.Id == 1);
            provider.SetEnabled(false);
            await context.SaveChangesAsync();
        }

        var sink = new CapturingSink();
        var controller = new AdminProviderFetchController(
            new FetchGuard(fixture.CreateContextFactory(), TimeProvider.System),
            new CapturingPublisher(),
            new FetchLifecycle(fixture.CreateContextFactory(), TimeProvider.System, sink),
            sink)
        {
            ControllerContext = new ControllerContext { HttpContext = CreateHttpContext("corr-guard") }
        };

        var result = await controller.Fetch(1, CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result.Result);
        var rejection = Assert.Single(sink.Events);
        Assert.Equal(FetchLifecycleEventNames.GuardRejection, rejection.EventName);
        Assert.Equal("Disabled", rejection.Reason);
        Assert.Equal("corr-guard", rejection.CorrelationId);
    }

    private static DefaultHttpContext CreateHttpContext(string correlationId)
    {
        var context = new DefaultHttpContext();
        context.Items["CorrelationId"] = correlationId;
        return context;
    }

    private sealed class CapturingSink : IFetchLifecycleEventSink
    {
        public List<FetchLifecycleEvent> Events { get; } = [];

        public void Emit(FetchLifecycleEvent lifecycleEvent) => Events.Add(lifecycleEvent);
    }

    private sealed class CapturingPublisher : IFetchJobPublisher
    {
        public FetchJob? Job { get; private set; }

        public Task PublishAsync(FetchJob job, CancellationToken cancellationToken)
        {
            Job = job;
            return Task.CompletedTask;
        }
    }
}
