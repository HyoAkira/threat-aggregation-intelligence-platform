using Microsoft.AspNetCore.Mvc;
using System.Globalization;
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

    private static readonly HashSet<string> Sources = new(StringComparer.OrdinalIgnoreCase)
    {
        "api", "worker", "all"
    };

    [HttpGet("logs")]
    public async Task<ActionResult<IReadOnlyList<AdminLogEntry>>> Logs(
        [FromQuery] int? count,
        [FromQuery] string? level,
        [FromQuery] string? source,
        [FromQuery] string? correlationId,
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

        var resolvedSource = string.IsNullOrWhiteSpace(source) ? "api" : source.Trim().ToLowerInvariant();
        if (!Sources.Contains(resolvedSource))
        {
            return ApiProblemFactory.Result(
                HttpContext,
                StatusCodes.Status400BadRequest,
                "invalid_log_query",
                "Invalid log query",
                "The source must be api, worker, or all.");
        }

        return Ok(await reader.ReadAsync(
            resolvedCount,
            string.IsNullOrWhiteSpace(level) ? null : level,
            resolvedSource,
            string.IsNullOrWhiteSpace(correlationId) ? null : correlationId,
            cancellationToken));
    }

    [HttpGet("logs/table")]
    public async Task<ActionResult<AdminLogPage>> Table(
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        [FromQuery] string? level,
        [FromQuery] string? source,
        [FromQuery] string? correlationId,
        [FromQuery] string? dateFrom,
        [FromQuery] string? dateTo,
        [FromQuery] string? orderDir,
        CancellationToken cancellationToken)
    {
        var resolvedPage = page ?? 1;
        var resolvedPageSize = pageSize ?? 20;
        if (resolvedPage < 1 || resolvedPageSize is < 1 or > 100)
        {
            return InvalidQuery("The page must be at least 1 and pageSize must be between 1 and 100.");
        }

        if (!TryParseDate(dateFrom, out var resolvedDateFrom)
            || !TryParseDate(dateTo, out var resolvedDateTo)
            || (resolvedDateFrom.HasValue && resolvedDateTo.HasValue && resolvedDateFrom > resolvedDateTo))
        {
            return InvalidQuery("The date range is invalid.");
        }

        var resolvedLevel = string.IsNullOrWhiteSpace(level) ? null : level;
        if (resolvedLevel is not null && !Levels.Contains(resolvedLevel))
        {
            return InvalidQuery("The level must be one of Verbose, Debug, Information, Warning, Error, or Fatal.");
        }

        var resolvedSource = string.IsNullOrWhiteSpace(source) ? "api" : source.Trim().ToLowerInvariant();
        if (!Sources.Contains(resolvedSource))
        {
            return InvalidQuery("The source must be api, worker, or all.");
        }

        var resolvedOrderDir = string.Equals(orderDir, "asc", StringComparison.OrdinalIgnoreCase)
            ? "asc"
            : "desc";

        return Ok(await reader.ReadPageAsync(
            resolvedPage,
            resolvedPageSize,
            resolvedLevel,
            resolvedSource,
            string.IsNullOrWhiteSpace(correlationId) ? null : correlationId,
            resolvedDateFrom,
            resolvedDateTo,
            resolvedOrderDir,
            cancellationToken));
    }

    private ActionResult InvalidQuery(string detail) => ApiProblemFactory.Result(
        HttpContext,
        StatusCodes.Status400BadRequest,
        "invalid_log_query",
        "Invalid log query",
        detail);

    private static bool TryParseDate(string? value, out DateTimeOffset? result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(value)) return true;

        if (!DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed))
        {
            return false;
        }

        result = parsed;
        return true;
    }
}
