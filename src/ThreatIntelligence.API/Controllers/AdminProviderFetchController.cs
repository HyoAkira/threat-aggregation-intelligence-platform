using Microsoft.AspNetCore.Mvc;
using ThreatIntelligence.API.Errors;
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
        QueueAsync(providerId, null, cancellationToken);

    [HttpPost("{providerId:int}/fetch/full")]
    public Task<ActionResult<FetchQueuedResponse>> FetchFull(int providerId, CancellationToken cancellationToken) =>
        QueueAsync(providerId, FetchMode.Full, cancellationToken);

    private async Task<ActionResult<FetchQueuedResponse>> QueueAsync(
        int providerId,
        FetchMode? mode,
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
                mode ?? FetchMode.Recent,
                FetchTrigger.Manual,
                correlationId,
                reservation.Rejection?.ToString());
            return reservation.Rejection switch
            {
                FetchReservationRejection.ProviderNotFound => ApiProblemFactory.Result(
                    HttpContext,
                    StatusCodes.Status404NotFound,
                    "provider_not_found",
                    "Provider not found",
                    "The requested provider could not be found."),
                FetchReservationRejection.UnsupportedMode => ApiProblemFactory.Result(
                    HttpContext,
                    StatusCodes.Status400BadRequest,
                    "unsupported_fetch_mode",
                    "Unsupported fetch mode",
                    "The provider does not support the requested fetch mode."),
                _ => ApiProblemFactory.Result(
                    HttpContext,
                    StatusCodes.Status409Conflict,
                    "fetch_guarded",
                    "Fetch unavailable",
                    "The fetch is currently unavailable because an execution guard is active.")
            };
        }

        var job = new FetchJob(reservation.JobId!.Value, providerId, FetchTrigger.Manual, reservation.Mode!.Value, 1, correlationId);
        Emit(FetchLifecycleEventNames.Reservation, job);
        try
        {
            await fetchJobPublisher.PublishAsync(job, cancellationToken);
        }
        catch (Exception exception)
        {
            await fetchLifecycle.FailAsync(job, $"Unable to publish fetch job: {exception.Message}", cancellationToken);
            return ApiProblemFactory.Result(
                HttpContext,
                StatusCodes.Status503ServiceUnavailable,
                "fetch_publish_failed",
                "Fetch unavailable",
                "The fetch job could not be queued at this time.");
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
