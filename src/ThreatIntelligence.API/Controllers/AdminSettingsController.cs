using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThreatIntelligence.Domain.Entities;
using ThreatIntelligence.API.Errors;
using ThreatIntelligence.Infrastructure.Persistence;

namespace ThreatIntelligence.API.Controllers;

[ApiController]
[Route("api/admin/settings")]
public sealed class AdminSettingsController(ThreatIntelligenceDbContext dbContext, TimeProvider timeProvider) : ControllerBase
{
    private static readonly HashSet<string> LogLevels = ["Verbose", "Debug", "Information", "Warning", "Error", "Fatal"];

    [HttpGet]
    public async Task<ActionResult<SettingsResponse>> Get(CancellationToken cancellationToken)
    {
        var logLevel = await dbContext.Settings.AsNoTracking()
            .Where(setting => setting.Key == "Logging:MinimumLevel")
            .Select(setting => setting.Value)
            .SingleOrDefaultAsync(cancellationToken) ?? "Information";
        var providers = await dbContext.Providers.AsNoTracking()
            .OrderBy(provider => provider.Id)
            .Select(provider => new ProviderSettingResponse(
                provider.Id,
                provider.Name,
                provider.IsEnabled,
                provider.IntervalMinutes,
                provider.MinimumIntervalMinutes,
                provider.RequiresApiKey))
            .ToListAsync(cancellationToken);
        return Ok(new SettingsResponse(logLevel, providers));
    }

    [HttpPut]
    public async Task<IActionResult> Put(SettingsRequest request, CancellationToken cancellationToken)
    {
        if (!LogLevels.Contains(request.LogLevel) || request.Providers.Count == 0)
        {
            return ApiProblemFactory.Result(
                HttpContext,
                StatusCodes.Status400BadRequest,
                "invalid_settings",
                "Invalid settings",
                "The runtime settings are invalid.");
        }

        var providers = await dbContext.Providers.Where(provider => request.Providers.Select(input => input.Id).Contains(provider.Id))
            .ToListAsync(cancellationToken);
        if (providers.Count != request.Providers.Count || request.Providers.Select(provider => provider.Id).Distinct().Count() != request.Providers.Count ||
            request.Providers.Any(input =>
                input.IntervalMinutes <
                (providers.SingleOrDefault(provider => provider.Id == input.Id)?.MinimumIntervalMinutes ?? int.MaxValue)))
        {
            return ApiProblemFactory.Result(
                HttpContext,
                StatusCodes.Status400BadRequest,
                "invalid_provider_settings",
                "Invalid provider settings",
                "One or more provider settings are invalid.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        foreach (var input in request.Providers)
        {
            var provider = providers.Single(provider => provider.Id == input.Id);
            provider.SetEnabled(input.IsEnabled);
            provider.SetIntervalMinutes(input.IntervalMinutes);
            provider.SetRequiresApiKey(input.RequiresApiKey);
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var logSetting = await dbContext.Settings.SingleOrDefaultAsync(setting => setting.Key == "Logging:MinimumLevel", cancellationToken);
        if (logSetting is null)
        {
            dbContext.Settings.Add(new Settings("Logging:MinimumLevel", request.LogLevel, now));
        }
        else
        {
            logSetting.Update(request.LogLevel, now);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return NoContent();
    }
}

public sealed record SettingsRequest(string LogLevel, List<ProviderSettingRequest> Providers);
public sealed record ProviderSettingRequest(int Id, bool IsEnabled, int IntervalMinutes, bool RequiresApiKey = false);
public sealed record SettingsResponse(string LogLevel, IReadOnlyList<ProviderSettingResponse> Providers);
public sealed record ProviderSettingResponse(
    int Id,
    string Name,
    bool IsEnabled,
    int IntervalMinutes,
    int MinimumIntervalMinutes,
    bool RequiresApiKey = false);
