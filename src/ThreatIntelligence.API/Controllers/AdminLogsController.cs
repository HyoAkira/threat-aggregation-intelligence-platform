using Microsoft.AspNetCore.Mvc;
using ThreatIntelligence.Application.Abstractions.Persistence;
using ThreatIntelligence.API.Errors;
using ThreatIntelligence.Application.Admin;

namespace ThreatIntelligence.API.Controllers;

[ApiController]
[Route("api/admin")]
public sealed class AdminLogsController(IAdminLogReader reader) : ControllerBase
{
    public const int DefaultCount = 100;
    public const int MaxCount = 500;

    private static readonly HashSet<string> Levels = new(StringComparer.OrdinalIgnoreCase)
    {
        "Verbose", "Debug", "Information", "Warning", "Error", "Fatal"
    };

    [HttpGet("logs")]
    public async Task<ActionResult<IReadOnlyList<AdminLogEntry>>> Logs(
        [FromQuery] int? count,
        [FromQuery] string? level,
        CancellationToken cancellationToken)
    {
        var resolvedCount = count ?? DefaultCount;
        if (resolvedCount is < 1 or > MaxCount)
        {
            return ApiProblemFactory.Result(
                HttpContext,
                StatusCodes.Status400BadRequest,
                "invalid_log_query",
                "Invalid log query",
                $"The count must be between 1 and {MaxCount}.");
        }

        if (!string.IsNullOrWhiteSpace(level) && !Levels.Contains(level))
        {
            return ApiProblemFactory.Result(
                HttpContext,
                StatusCodes.Status400BadRequest,
                "invalid_log_query",
                "Invalid log query",
                "The level must be one of Verbose, Debug, Information, Warning, Error, or Fatal.");
        }

        return Ok(await reader.ReadAsync(
            resolvedCount,
            string.IsNullOrWhiteSpace(level) ? null : level,
            cancellationToken));
    }
}
