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
    IFetchLifecycle fetchLifecycle) : ControllerBase
{
    [HttpPost("{providerId:int}/fetch")]
    public Task<ActionResult<FetchQueuedResponse>> Fetch(int providerId, CancellationToken cancellationToken) =>
        QueueAsync(providerId, null, cancellationToken);

    [HttpPost("{providerId:int}/fetch/full")]
    public Task<ActionResult<FetchQueuedResponse>> FetchFull(int providerId, CancellationToken cancellationToken) =>
        QueueAsync(providerId, FetchMode.Full, cancellationToken);

    private async Task<ActionResult<FetchQueuedResponse>> QueueAsync(
        int providerId,
        FetchMode? requestedMode,
        CancellationToken cancellationToken)
    {
        var defaultMode = providerId == 1 ? FetchMode.Recent : FetchMode.Full;
        var mode = requestedMode ?? defaultMode;
        if (requestedMode == FetchMode.Full && providerId != 1)
        {
            return BadRequest(new ProblemDetails { Title = "Full fetch shortcut is only available for ThreatFox." });
        }

        var reservation = await fetchGuard.ReserveAsync(providerId, FetchTrigger.Manual, mode, cancellationToken);
        if (!reservation.IsAllowed)
        {
            return reservation.Rejection == FetchReservationRejection.ProviderNotFound
                ? NotFound()
                : reservation.Rejection == FetchReservationRejection.UnsupportedMode
                    ? BadRequest(new ProblemDetails { Title = "The provider does not support the requested fetch mode." })
                    : Conflict(new ProblemDetails { Title = "The fetch is currently guarded.", Detail = reservation.Rejection.ToString() });
        }

        var job = new FetchJob(reservation.JobId!.Value, providerId, FetchTrigger.Manual, mode, 1);
        try
        {
            await fetchJobPublisher.PublishAsync(job, cancellationToken);
        }
        catch (Exception exception)
        {
            await fetchLifecycle.FailAsync(job.JobId, $"Unable to publish fetch job: {exception.Message}", cancellationToken);
            return Problem(title: "Unable to publish fetch job.", statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        return Accepted(new FetchQueuedResponse(job.JobId, "queued"));
    }
}

public sealed record FetchQueuedResponse(Guid JobId, string Status);
