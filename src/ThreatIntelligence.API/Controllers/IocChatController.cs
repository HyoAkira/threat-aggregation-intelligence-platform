using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThreatIntelligence.API.Errors;
using ThreatIntelligence.Application.AI;
using ThreatIntelligence.Infrastructure.AI;
using ThreatIntelligence.API.Chat;

namespace ThreatIntelligence.API.Controllers;

[ApiController]
[Route("api/iocs/{id:int}/chat")]
[AllowAnonymous]
public sealed class IocChatController(IocChatService chatService, ChatRateLimiter rateLimiter) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<IocChatResponse>> Post(int id, IocChatRequest request, CancellationToken cancellationToken)
    {
        var clientKey = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var lease = await rateLimiter.AcquireAsync(clientKey, cancellationToken);
        if (lease is null)
        {
            Response.Headers.RetryAfter = "5";
            return ApiProblemFactory.Result(HttpContext, StatusCodes.Status429TooManyRequests, "ioc_chat_rate_limited", "IOC chat busy", "Try again shortly.");
        }
        using (lease)
        {
        try
        {
            var result = await chatService.AskAsync(id, request.Message ?? string.Empty, request.History, cancellationToken);
            return Ok(new IocChatResponse(result.Answer, result.IocIds, result.ProviderRecordIds, result.HasAssessment));
        }
        catch (IocChatException exception)
        {
            return ApiProblemFactory.Result(HttpContext, exception.StatusCode, exception.Code, "IOC chat unavailable", exception.Message);
        }
        catch (AiConfigurationException exception)
        {
            return ApiProblemFactory.Result(HttpContext, StatusCodes.Status503ServiceUnavailable, exception.Code, "IOC chat unavailable", exception.Message);
        }
        catch (AiUpstreamException exception)
        {
            var status = exception.Code == AiErrorCodes.UpstreamTimeout
                ? StatusCodes.Status504GatewayTimeout
                : StatusCodes.Status503ServiceUnavailable;
            return ApiProblemFactory.Result(HttpContext, status, exception.Code, "IOC chat unavailable", exception.Message);
        }
        }
    }
}

public sealed record IocChatRequest(string? Message, IReadOnlyList<IocChatTurn>? History);
public sealed record IocChatResponse(string Answer, IReadOnlyList<int> IocIds, IReadOnlyList<string> ProviderRecordIds, bool HasAssessment);
