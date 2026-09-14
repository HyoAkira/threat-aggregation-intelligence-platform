using Microsoft.AspNetCore.Mvc;
using ThreatIntelligence.Application.Abstractions.Persistence;
using ThreatIntelligence.Application.Admin;
using ThreatIntelligence.API.Errors;

namespace ThreatIntelligence.API.Controllers;

/// <summary>
/// Admin provider operations surface. The <c>api/admin</c> route prefix is what the
/// <see cref="Authorization.AdminControllerConvention"/> uses to require the Admin role.
/// </summary>
[ApiController]
[Route("api/admin")]
public sealed class AdminProviderOperationsController(
    IAdminProviderOperationsReader reader,
    IAdminProviderRepository repository) : ControllerBase
{
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    [HttpGet("providers")]
    public async Task<ActionResult<IReadOnlyList<AdminProviderDashboardItem>>> Providers(CancellationToken cancellationToken) =>
        Ok(await reader.GetProvidersAsync(cancellationToken));

    [HttpPost("providers")]
    public async Task<ActionResult<AdminProviderDashboardItem>> Create(
        ProviderWriteRequest request,
        CancellationToken cancellationToken)
    {
        var result = await repository.CreateAsync(request, cancellationToken);
        return result.Status switch
        {
            ProviderWriteStatus.Success => Created(
                $"/api/admin/providers/{result.Provider!.Id}", result.Provider),
            ProviderWriteStatus.DuplicateName => ApiProblemFactory.Result(
                HttpContext,
                StatusCodes.Status400BadRequest,
                "duplicate_provider_name",
                "Duplicate provider name",
                "A provider with the same name already exists."),
            _ => InvalidProvider()
        };
    }

    [HttpPut("providers/{providerId:int}")]
    public async Task<ActionResult<AdminProviderDashboardItem>> Update(
        int providerId,
        ProviderWriteRequest request,
        CancellationToken cancellationToken)
    {
        var result = await repository.UpdateAsync(providerId, request, cancellationToken);
        return result.Status switch
        {
            ProviderWriteStatus.Success => Ok(result.Provider),
            ProviderWriteStatus.NotFound => ProviderNotFound(),
            ProviderWriteStatus.DuplicateName => ApiProblemFactory.Result(
                HttpContext,
                StatusCodes.Status400BadRequest,
                "duplicate_provider_name",
                "Duplicate provider name",
                "A provider with the same name already exists."),
            _ => InvalidProvider()
        };
    }

    [HttpDelete("providers/{providerId:int}")]
    public async Task<ActionResult> Delete(int providerId, CancellationToken cancellationToken)
    {
        var result = await repository.DeleteAsync(providerId, cancellationToken);
        return result.Status switch
        {
            ProviderWriteStatus.Success => NoContent(),
            ProviderWriteStatus.NotFound => ProviderNotFound(),
            ProviderWriteStatus.InUse => ApiProblemFactory.Result(
                HttpContext,
                StatusCodes.Status409Conflict,
                "provider_in_use",
                "Provider in use",
                "The provider still owns fetch history or IOC records and cannot be deleted."),
            _ => InvalidProvider()
        };
    }

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
            ? ProviderNotFound()
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

    private ActionResult InvalidProvider() => ApiProblemFactory.Result(
        HttpContext,
        StatusCodes.Status400BadRequest,
        "invalid_provider",
        "Invalid provider",
        "The submitted provider configuration is invalid.");

    private ActionResult ProviderNotFound() => ApiProblemFactory.Result(
        HttpContext,
        StatusCodes.Status404NotFound,
        "provider_not_found",
        "Provider not found",
        "The requested provider could not be found.");
}
