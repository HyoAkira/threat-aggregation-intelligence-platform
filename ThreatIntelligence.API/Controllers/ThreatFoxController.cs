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
        try
        {
            var count = await _mediator.Send(new DownloadThreatFoxDatasetCommand());
            return Ok(new { Message = "Download completed", IocsProcessed = count });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not configured"))
        {
            return Unauthorized(new { Error = ex.Message });
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(502, new { Error = ex.Message });
        }
    }

    [HttpGet("recent")]
    public async Task<IActionResult> GetRecentIoCs()
    {
        try
        {
            var count = await _mediator.Send(new GetRecentThreatFoxIoCsCommand());
            return Ok(new { Message = "Recent IOCs fetched", IocsProcessed = count });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not configured"))
        {
            return Unauthorized(new { Error = ex.Message });
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(502, new { Error = ex.Message });
        }
    }
}
