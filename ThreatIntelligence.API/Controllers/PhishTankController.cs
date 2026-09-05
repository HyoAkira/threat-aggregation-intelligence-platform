using MediatR;
using Microsoft.AspNetCore.Mvc;
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
    public async Task<IActionResult> DownloadDataset()
    {
        try
        {
            var count = await _mediator.Send(new DownloadPhishTankDatasetCommand());
            return Ok(new { Message = "Download completed", PhishEntriesProcessed = count });
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(502, new { Error = ex.Message });
        }
    }
}
