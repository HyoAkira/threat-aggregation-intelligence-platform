using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using ThreatIntelligence.Infrastructure;

namespace ThreatIntelligence.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PhishTankController : ControllerBase
{
    private readonly PhishTankApiService _service;
    private readonly IConfiguration _configuration;

    public PhishTankController(PhishTankApiService service, IConfiguration configuration)
    {
        _service = service;
        _configuration = configuration;
    }

    [HttpGet("download")]
    public async Task<IActionResult> DownloadDataset()
    {
        try
        {
            var count = await _service.DownloadDatasetAsync();
            return Ok(new { Message = "Download completed", PhishEntriesProcessed = count });
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(502, new { Error = ex.Message });
        }
    }
}
