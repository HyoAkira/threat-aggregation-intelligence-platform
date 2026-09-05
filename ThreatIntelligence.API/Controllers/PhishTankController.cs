using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ThreatIntelligence.Application.DTOs;
using ThreatIntelligence.Application.Features.PhishTank.DownloadDataset;

namespace ThreatIntelligence.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PhishTankController : ControllerBase
{
    private readonly IMediator _mediator;

    public PhishTankController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("download")]
    [ProducesResponseType<DownloadDatasetResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    public async Task<ActionResult<DownloadDatasetResponse>> DownloadDataset()
    {
        var response = await _mediator.Send(new DownloadPhishTankDatasetCommand());
        return Ok(response);
    }
}