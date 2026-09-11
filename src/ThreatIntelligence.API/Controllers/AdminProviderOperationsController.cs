using Microsoft.AspNetCore.Mvc;
using ThreatIntelligence.Application.Abstractions.Persistence;
using ThreatIntelligence.Application.Admin;
using ThreatIntelligence.API.Errors;

namespace ThreatIntelligence.API.Controllers;

/// <summary>
/// Admin provider operations read surface. The <c>api/admin</c> route prefix is what the
/// <see cref="Authorization.AdminControllerConvention"/> uses to require the Admin role.
/// </summary>
[ApiController]
[Route("api/admin")]
public sealed class AdminProviderOperationsController(IAdminProviderOperationsReader reader) : ControllerBase
{
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    [HttpGet("providers")]
    public async Task<ActionResult<IReadOnlyList<AdminProviderDashboardItem>>> Providers(CancellationToken cancellationToken) =>
        Ok(await reader.GetProvidersAsync(cancellationToken));

    [HttpGet("providers/{providerId:int}/history")]
    public async Task<ActionResult<AdminProviderHistoryPage>> History(
        int providerId,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        var resolvedPage = page ?? DefaultPage;
        var resolvedPageSize = pageSize ?? DefaultPageSize;
        if (resolvedPage < 1 || resolvedPageSize is < 1 or > MaxPageSize)
        {
            return ApiProblemFactory.Result(
                HttpContext,
                StatusCodes.Status400BadRequest,
                "invalid_paging",
                "Invalid paging",
                $"The page must be at least 1 and pageSize must be between 1 and {MaxPageSize}.");
        }

        var result = await reader.GetProviderHistoryAsync(providerId, resolvedPage, resolvedPageSize, cancellationToken);
        return result is null
            ? ApiProblemFactory.Result(
                HttpContext,
                StatusCodes.Status404NotFound,
                "provider_not_found",
                "Provider not found",
                "The requested provider could not be found.")
            : Ok(result);
    }

    [HttpGet("fetch-history/{jobId:guid}")]
    public async Task<ActionResult<AdminFetchHistoryItem>> Job(Guid jobId, CancellationToken cancellationToken)
    {
        var result = await reader.GetFetchHistoryAsync(jobId, cancellationToken);
        return result is null
            ? ApiProblemFactory.Result(
                HttpContext,
                StatusCodes.Status404NotFound,
                "fetch_job_not_found",
                "Fetch job not found",
                "The requested fetch job could not be found.")
            : Ok(result);
    }
}
