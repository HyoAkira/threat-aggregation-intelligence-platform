using Microsoft.AspNetCore.Mvc;
using ThreatIntelligence.API.Controllers;
using ThreatIntelligence.API.Errors;
using ThreatIntelligence.Application.Fetches;
using ThreatIntelligence.Domain.Enums;

namespace ThreatIntelligence.IntegrationTests;

public sealed class AdminProviderFetchControllerTests
{
    [Fact]
    public async Task Default_fetch_publishes_a_queued_manual_job_and_returns_202()
    {
        var guard = new StubGuard(FetchReservation.Allow(Guid.Parse("969d935c-3c0b-4cd2-90a4-4917ff6c8ef7"), FetchMode.Recent));
        var publisher = new CapturingPublisher();
        var controller = new AdminProviderFetchController(guard, publisher, new StubLifecycle());

        var result = await controller.Fetch(1, CancellationToken.None);

        var accepted = Assert.IsType<AcceptedResult>(result.Result);
        var response = Assert.IsType<FetchQueuedResponse>(accepted.Value);
        Assert.Equal("queued", response.Status);
        Assert.Equal(response.JobId, publisher.Job!.JobId);
        Assert.Equal(FetchTrigger.Manual, publisher.Job.Trigger);
        Assert.Equal(FetchMode.Recent, publisher.Job.Mode);
    }

    [Fact]
    public async Task Missing_provider_returns_404_without_publishing()
    {
        var publisher = new CapturingPublisher();
        var controller = new AdminProviderFetchController(
            new StubGuard(FetchReservation.Reject(FetchReservationRejection.ProviderNotFound)), publisher, new StubLifecycle());

        var result = await controller.Fetch(99, CancellationToken.None);

        var problem = Assert.IsType<NotFoundObjectResult>(result.Result);
        Assert.Equal("provider_not_found", Assert.IsType<ApiProblemDetails>(problem.Value).Code);
        Assert.Null(publisher.Job);
    }

    [Fact]
    public async Task Guarded_fetch_returns_409_without_publishing()
    {
        var publisher = new CapturingPublisher();
        var controller = new AdminProviderFetchController(
            new StubGuard(FetchReservation.Reject(FetchReservationRejection.ActiveFetch)), publisher, new StubLifecycle());

        var result = await controller.Fetch(1, CancellationToken.None);

        var problem = Assert.IsType<ConflictObjectResult>(result.Result);
        Assert.Equal("fetch_guarded", Assert.IsType<ApiProblemDetails>(problem.Value).Code);
        Assert.Null(publisher.Job);
    }

    [Fact]
    public async Task Full_fetch_shortcut_queues_full_mode_for_any_provider_the_guard_admits()
    {
        var publisher = new CapturingPublisher();
        var controller = new AdminProviderFetchController(
            new StubGuard(FetchReservation.Allow(Guid.NewGuid(), FetchMode.Full)), publisher, new StubLifecycle());

        var result = await controller.FetchFull(2, CancellationToken.None);

        var accepted = Assert.IsType<AcceptedResult>(result.Result);
        var response = Assert.IsType<FetchQueuedResponse>(accepted.Value);
        Assert.Equal("queued", response.Status);
        Assert.Equal(response.JobId, publisher.Job!.JobId);
        Assert.Equal(FetchMode.Full, publisher.Job.Mode);
    }

    [Fact]
    public async Task Recent_fetch_for_a_full_only_provider_returns_400_without_publishing()
    {
        var publisher = new CapturingPublisher();
        var controller = new AdminProviderFetchController(
            new StubGuard(FetchReservation.Reject(FetchReservationRejection.UnsupportedMode)), publisher, new StubLifecycle());

        var result = await controller.Fetch(2, CancellationToken.None);

        var problem = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("unsupported_fetch_mode", Assert.IsType<ApiProblemDetails>(problem.Value).Code);
        Assert.Null(publisher.Job);
    }

    [Fact]
    public async Task Publish_failure_marks_the_reserved_history_failed_and_does_not_return_202()
    {
        var jobId = Guid.NewGuid();
        var lifecycle = new CapturingLifecycle();
        var controller = new AdminProviderFetchController(
            new StubGuard(FetchReservation.Allow(jobId, FetchMode.Recent)), new FailingPublisher(), lifecycle);

        var result = await controller.Fetch(1, CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(503, problem.StatusCode);
        Assert.Equal("fetch_publish_failed", Assert.IsType<ApiProblemDetails>(problem.Value).Code);
        Assert.Equal(jobId, lifecycle.FailedJobId);
        Assert.Contains("broker unavailable", lifecycle.ErrorDetail, StringComparison.Ordinal);
    }

    private sealed class StubGuard(FetchReservation reservation) : IFetchGuard
    {
        public Task<FetchReservation> ReserveAsync(int providerId, FetchTrigger trigger, FetchMode? mode, CancellationToken cancellationToken) =>
            Task.FromResult(reservation);

        public Task<bool> CanExecuteAsync(FetchJob job, CancellationToken cancellationToken) => Task.FromResult(true);
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

    private sealed class FailingPublisher : IFetchJobPublisher
    {
        public Task PublishAsync(FetchJob job, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("broker unavailable");
    }

    private sealed class StubLifecycle : IFetchLifecycle
    {
        public Task<bool> TryStartAsync(FetchJob job, bool isRetry, CancellationToken cancellationToken) => Task.FromResult(true);

        public Task SucceedAsync(FetchJob job, int recordsAdded, int recordsUpdated, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task FailAsync(FetchJob job, string errorDetail, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class CapturingLifecycle : IFetchLifecycle
    {
        public Guid? FailedJobId { get; private set; }
        public string ErrorDetail { get; private set; } = string.Empty;

        public Task<bool> TryStartAsync(FetchJob job, bool isRetry, CancellationToken cancellationToken) => Task.FromResult(true);
        public Task SucceedAsync(FetchJob job, int recordsAdded, int recordsUpdated, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task FailAsync(FetchJob job, string errorDetail, CancellationToken cancellationToken)
        {
            FailedJobId = job.JobId;
            ErrorDetail = errorDetail;
            return Task.CompletedTask;
        }
    }
}
