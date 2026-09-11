using Microsoft.AspNetCore.Mvc;
using ThreatIntelligence.Application.Fetches;
using ThreatIntelligence.Domain.Enums;
using ThreatIntelligence.Infrastructure.Fetches;

namespace ThreatIntelligence.API.Controllers;

[ApiController]
[Route("api/admin/providers")]
public sealed class AdminProviderFetchController(
    IFetchGuard fetchGuard,
    IFetchJobPublisher fetchJobPublisher,
    IFetchLifecycle fetchLifecycle,
    IFetchLifecycleEventSink? eventSink = null) : ControllerBase
{
    private readonly IFetchLifecycleEventSink _eventSink = eventSink ?? NullFetchLifecycleEventSink.Instance;

    [HttpPost("{providerId:int}/fetch")]
    public Task<ActionResult<FetchQueuedResponse>> Fetch(int providerId, CancellationToken cancellationToken) =>
        QueueAsync(providerId, FetchMode.Recent, cancellationToken);

    [HttpPost("{providerId:int}/fetch/full")]
    public Task<ActionResult<FetchQueuedResponse>> FetchFull(int providerId, CancellationToken cancellationToken) =>
        QueueAsync(providerId, FetchMode.Full, cancellationToken);

    private async Task<ActionResult<FetchQueuedResponse>> QueueAsync(
        int providerId,
        FetchMode mode,
        CancellationToken cancellationToken)
    {
        var correlationId = HttpContext?.Items["CorrelationId"] as string;
        var reservation = await fetchGuard.ReserveAsync(providerId, FetchTrigger.Manual, mode, cancellationToken);
        if (!reservation.IsAllowed)
        {
            Emit(
                FetchLifecycleEventNames.GuardRejection,
                null,
                providerId,
                mode,
                FetchTrigger.Manual,
                correlationId,
                reservation.Rejection?.ToString());
            return reservation.Rejection == FetchReservationRejection.ProviderNotFound
                ? NotFound()
                : reservation.Rejection == FetchReservationRejection.UnsupportedMode
                    ? BadRequest(new ProblemDetails { Title = "The provider does not support the requested fetch mode." })
                    : Conflict(new ProblemDetails { Title = "The fetch is currently guarded.", Detail = reservation.Rejection.ToString() });
        }

        var job = new FetchJob(reservation.JobId!.Value, providerId, FetchTrigger.Manual, mode, 1, correlationId);
        Emit(FetchLifecycleEventNames.Reservation, job);
        try
        {
            await fetchJobPublisher.PublishAsync(job, cancellationToken);
        }
        catch (Exception exception)
        {
            await fetchLifecycle.FailAsync(job, $"Unable to publish fetch job: {exception.Message}", cancellationToken);
            return Problem(title: "Unable to publish fetch job.", statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        return Accepted(new FetchQueuedResponse(job.JobId, "queued"));
    }

    private void Emit(
        string eventName,
        FetchJob? job,
        int providerId,
        FetchMode mode,
        FetchTrigger trigger,
        string? correlationId,
        string? reason = null) =>
        _eventSink.Emit(new FetchLifecycleEvent(
            eventName,
            job?.JobId,
            providerId,
            FetchTokens.Mode(mode),
            FetchTokens.Trigger(trigger),
            job?.CorrelationId ?? correlationId ?? Guid.NewGuid().ToString("N"),
            Reason: reason));

    private void Emit(string eventName, FetchJob job) =>
        _eventSink.Emit(new FetchLifecycleEvent(
            eventName,
            job.JobId,
            job.ProviderId,
            FetchTokens.Mode(job.Mode),
            FetchTokens.Trigger(job.Trigger),
            job.CorrelationId));
}

public sealed record FetchQueuedResponse(Guid JobId, string Status);
