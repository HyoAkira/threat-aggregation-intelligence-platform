using Microsoft.AspNetCore.Mvc;
using ThreatIntelligence.API.Errors;
using ThreatIntelligence.Application.Abstractions.Persistence;
using ThreatIntelligence.Application.Abstractions.Time;
using ThreatIntelligence.Application.Admin;

namespace ThreatIntelligence.API.Controllers;

[ApiController]
[Route("api/admin")]
public sealed class AdminSummaryController(IAdminSummaryReader reader) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<ActionResult<AdminSummary>> Summary(CancellationToken cancellationToken) =>
        Ok(await reader.GetSummaryAsync(cancellationToken));

    /// <summary>
    /// Bounded IOC activity timeline over persisted fetch-history counters. The reader and timezone
    /// resolver are action-scoped so the established summary constructor contract is unchanged.
    /// </summary>
    [HttpGet("summary/timeline")]
    public async Task<ActionResult<AdminTimeline>> Timeline(
        [FromQuery] string? from,
        [FromQuery] string? to,
        [FromQuery] string? timeZone,
        [FromServices] IAdminSummaryTimelineReader timelineReader,
        [FromServices] ITimeZoneResolver timeZoneResolver,
        CancellationToken cancellationToken)
    {
        var validation = TimelineRequestValidator.Validate(from, to, timeZone, timeZoneResolver);
        if (!validation.IsValid)
        {
            return ApiProblemFactory.Result(
                HttpContext,
                StatusCodes.Status400BadRequest,
                validation.ErrorCode!,
                "Invalid timeline query",
                validation.ErrorDetail!);
        }

        var timeline = await timelineReader.GetTimelineAsync(
            validation.FromUtc,
            validation.ToUtc,
            validation.TimeZone!,
            cancellationToken);
        return Ok(timeline);
    }
}
