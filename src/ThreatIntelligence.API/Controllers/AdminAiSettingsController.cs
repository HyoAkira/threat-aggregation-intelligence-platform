using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThreatIntelligence.API.Errors;
using ThreatIntelligence.Application.Abstractions.Secrets;
using ThreatIntelligence.Domain.Entities;
using ThreatIntelligence.Application.AI;
using ThreatIntelligence.Infrastructure.Persistence;

namespace ThreatIntelligence.API.Controllers;

[ApiController]
[Route("api/admin/ai/settings")]
public sealed class AdminAiSettingsController(
    ThreatIntelligenceDbContext dbContext,
    ISecretStore secretStore,
    TimeProvider timeProvider) : ControllerBase
{
    private const int MaxBaseUrlLength = 2048;
    private const int MaxModelIdLength = 200;

    [HttpGet]
    public async Task<ActionResult<AiSettingsResponse>> Get(CancellationToken cancellationToken)
    {
        var settings = await ReadSettingsAsync(cancellationToken);
        var credential = await secretStore.GetStatusAsync(ManagedSecretNames.OpenAiCompatibleServerApiKey, cancellationToken);
        return Ok(new AiSettingsResponse(settings?.BaseUrl, settings?.ModelId, settings?.Enabled ?? false, credential is not null));
    }

    [HttpPut]
    public async Task<ActionResult<AiSettingsResponse>> Put(AiSettingsRequest request, CancellationToken cancellationToken)
    {
        var baseUrlError = ValidateBaseUrl(request.BaseUrl);
        if (baseUrlError is not null)
        {
            return Invalid(baseUrlError);
        }

        var modelId = request.ModelId?.Trim();
        if (modelId is { Length: > MaxModelIdLength })
        {
            return Invalid($"The model ID must be at most {MaxModelIdLength} characters.");
        }

        if (request.Enabled && (string.IsNullOrWhiteSpace(request.BaseUrl) || string.IsNullOrWhiteSpace(modelId)))
        {
            return Invalid("A non-empty Base URL and model ID are required while AI analysis is enabled.");
        }

        if (request.Enabled && !UrlIsUsable(request.BaseUrl!))
        {
            return Invalid("A reachable absolute HTTP or HTTPS Base URL is required while AI analysis is enabled.");
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var settings = await ReadSettingsAsync(cancellationToken);
        if (settings is null)
        {
            settings = new AiConnectionSettings(request.BaseUrl?.Trim(), modelId, request.Enabled, now);
            dbContext.AiConnectionSettings.Add(settings);
        }
        else
        {
            settings.Update(request.BaseUrl?.Trim(), modelId, request.Enabled, now);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var credential = await secretStore.GetStatusAsync(ManagedSecretNames.OpenAiCompatibleServerApiKey, cancellationToken);
        return Ok(new AiSettingsResponse(settings.BaseUrl, settings.ModelId, settings.Enabled, credential is not null));
    }

    private async Task<AiConnectionSettings?> ReadSettingsAsync(CancellationToken cancellationToken) =>
        await dbContext.AiConnectionSettings.SingleOrDefaultAsync(cancellationToken);

    private static bool UrlIsUsable(string baseUrl)
    {
        try
        {
            AiChatCompletionsUrl.Build(baseUrl);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static string? ValidateBaseUrl(string? baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return null;
        }

        if (baseUrl.Trim().Length > MaxBaseUrlLength)
        {
            return $"The Base URL must be at most {MaxBaseUrlLength} characters.";
        }

        try
        {
            AiChatCompletionsUrl.Build(baseUrl);
            return null;
        }
        catch (ArgumentException)
        {
            return "The Base URL must be an absolute HTTP or HTTPS URL.";
        }
    }

    private ActionResult Invalid(string detail) => ApiProblemFactory.Result(
        HttpContext,
        StatusCodes.Status400BadRequest,
        "invalid_ai_settings",
        "Invalid AI settings",
        detail);
}

public sealed record AiSettingsRequest(string? BaseUrl, string? ModelId, bool Enabled);
public sealed record AiSettingsResponse(string? BaseUrl, string? ModelId, bool Enabled, bool ApiKeyConfigured);
