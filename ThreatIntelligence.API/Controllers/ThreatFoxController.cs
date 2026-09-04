using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using ThreatIntelligence.Infrastructure;

namespace ThreatIntelligence.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ThreatFoxController : ControllerBase
{
    private readonly ThreatFoxApiService _service;
    private readonly IConfiguration _configuration;

    public ThreatFoxController(ThreatFoxApiService service, IConfiguration configuration)
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
            var count = await _service.RecentIoCsAsync();
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
