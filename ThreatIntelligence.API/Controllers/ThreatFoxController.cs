using MediatR;
using Microsoft.AspNetCore.Mvc;
using ThreatIntelligence.Application.Features.ThreatFox.DownloadDataset;
using ThreatIntelligence.Application.Features.ThreatFox.GetRecentIoCs;

namespace ThreatIntelligence.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ThreatFoxController : ControllerBase
{
    private readonly IMediator _mediator;

    public ThreatFoxController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("download")]
    public async Task<IActionResult> DownloadDataset()
    {
        var response = await _mediator.Send(new DownloadThreatFoxDatasetCommand());
        return Ok(response);
    }

    [HttpGet("recent")]
    public async Task<IActionResult> GetRecentIoCs()
    {
        var response = await _mediator.Send(new GetRecentThreatFoxIoCsCommand());
        return Ok(response);
    }
}