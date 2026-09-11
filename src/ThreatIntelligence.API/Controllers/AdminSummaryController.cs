using Microsoft.AspNetCore.Mvc;
using ThreatIntelligence.Application.Abstractions.Persistence;
using ThreatIntelligence.Application.Admin;

namespace ThreatIntelligence.API.Controllers;

[ApiController]
[Route("api/admin")]
public sealed class AdminSummaryController(IAdminSummaryReader reader) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<ActionResult<AdminSummary>> Summary(CancellationToken cancellationToken) =>
        Ok(await reader.GetSummaryAsync(cancellationToken));
}
