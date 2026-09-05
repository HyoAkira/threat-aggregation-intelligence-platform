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
        var response = await _mediator.Send(new DownloadPhishTankDatasetCommand());
        return Ok(response);
    }
}