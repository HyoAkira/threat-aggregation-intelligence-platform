using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThreatIntelligence.API.Errors;
using ThreatIntelligence.Application.Abstractions.Secrets;
using ThreatIntelligence.Application.AI;
using ThreatIntelligence.Domain.Entities;
using ThreatIntelligence.Infrastructure.Persistence;

namespace ThreatIntelligence.API.Controllers;

[ApiController]
[Route("api/admin/ioc-chat/settings")]
public sealed class AdminIocChatSettingsController(
    ThreatIntelligenceDbContext dbContext,
    ISecretStore secretStore,
    TimeProvider timeProvider) : ControllerBase
{
    private const int MaxBaseUrlLength = 2048;
    private const int MaxModelIdLength = 200;

    [HttpGet]
    public async Task<ActionResult<IocChatSettingsResponse>> Get(CancellationToken cancellationToken)
    {
        var settings = await ReadSettingsAsync(cancellationToken);
        var credential = await secretStore.GetStatusAsync(
            ManagedSecretNames.IocChatOpenAiCompatibleServerApiKey,
            cancellationToken);
        return Ok(new IocChatSettingsResponse(
            settings?.BaseUrl,
            settings?.ModelId,
            settings?.Enabled ?? false,
            credential is not null));
    }

    [HttpPut]
    public async Task<ActionResult<IocChatSettingsResponse>> Put(
        IocChatSettingsRequest request,
        CancellationToken cancellationToken)
    {
        var baseUrl = request.BaseUrl?.Trim();
        var modelId = request.ModelId?.Trim();

        if (baseUrl is { Length: > MaxBaseUrlLength })
        {
            return Invalid($"The Base URL must be at most {MaxBaseUrlLength} characters.");
        }
        if (modelId is { Length: > MaxModelIdLength })
        {
            return Invalid($"The model ID must be at most {MaxModelIdLength} characters.");
        }
        if (!string.IsNullOrWhiteSpace(baseUrl) && !IsUsableBaseUrl(baseUrl))
        {
            return Invalid("The Base URL must be an absolute HTTP or HTTPS URL.");
        }
        if (request.Enabled && (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(modelId)))
        {
            return Invalid("A non-empty Base URL and model ID are required while IOC chat is enabled.");
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var settings = await ReadSettingsAsync(cancellationToken);
        if (settings is null)
        {
            settings = new IocChatConnectionSettings(baseUrl, modelId, request.Enabled, now);
            dbContext.IocChatConnectionSettings.Add(settings);
        }
        else
        {
            settings.Update(baseUrl, modelId, request.Enabled, now);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        var credential = await secretStore.GetStatusAsync(
            ManagedSecretNames.IocChatOpenAiCompatibleServerApiKey,
            cancellationToken);
        return Ok(new IocChatSettingsResponse(settings.BaseUrl, settings.ModelId, settings.Enabled, credential is not null));
    }

    private Task<IocChatConnectionSettings?> ReadSettingsAsync(CancellationToken cancellationToken) =>
        dbContext.IocChatConnectionSettings.SingleOrDefaultAsync(cancellationToken);

    private static bool IsUsableBaseUrl(string baseUrl)
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

    private ActionResult Invalid(string detail) => ApiProblemFactory.Result(
        HttpContext,
        StatusCodes.Status400BadRequest,
        "invalid_ioc_chat_settings",
        "Invalid IOC chat settings",
        detail);
}

public sealed record IocChatSettingsRequest(string? BaseUrl, string? ModelId, bool Enabled);
public sealed record IocChatSettingsResponse(string? BaseUrl, string? ModelId, bool Enabled, bool ApiKeyConfigured);