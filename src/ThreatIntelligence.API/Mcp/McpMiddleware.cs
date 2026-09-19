using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace ThreatIntelligence.API.Mcp;

/// <summary>
/// Middleware that applies rate limiting and validation to MCP requests.
/// </summary>
public sealed class McpMiddleware(
    RequestDelegate next,
    IOptions<McpConfiguration> configuration,
    ILogger<McpMiddleware> logger)
{
    private const int MaxRequestSize = 1_000_000;

    public async Task InvokeAsync(HttpContext context, McpRateLimiter rateLimiter)
    {
        // Only apply to /mcp path
        if (!context.Request.Path.StartsWithSegments("/mcp"))
        {
            await next(context);
            return;
        }

        var config = configuration.Value;
        if (!config.Enabled)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        if (config.AcceptedHosts.Count > 0 &&
            !config.AcceptedHosts.Contains(context.Request.Host.Host, StringComparer.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        var origin = context.Request.Headers.Origin.ToString();
        if (!string.IsNullOrWhiteSpace(origin) &&
            (config.AllowedOrigins.Count == 0 || !config.AllowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase)))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        // Extract client ID for rate limiting.
        var clientId = McpRateLimiter.ExtractClientId(context.Request, config.TrustedProxyIps);

        // Apply rate limiting
        var (scope, retryAfter) = await rateLimiter.TryAcquireAsync(clientId, context.RequestAborted);
        if (scope is null)
        {
            logger.LogWarning("MCP rate limit exceeded for client {ClientId}", clientId);
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.Headers["Retry-After"] = Math.Max(1, (int)retryAfter).ToString();
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                error = "rate_limit_exceeded",
                message = "Too many MCP requests. Please try again later.",
                retryAfterSeconds = retryAfter
            }, context.RequestAborted);
            return;
        }

        using (scope)
        {
            // Check request size limit
            if (context.Request.ContentLength > MaxRequestSize)
            {
                logger.LogWarning("MCP request too large from client {ClientId}: {Size} bytes", clientId, context.Request.ContentLength);
                context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "payload_too_large",
                    message = $"The request body exceeds the maximum size of {MaxRequestSize} bytes."
                }, context.RequestAborted);
                return;
            }

            // Set timeout for MCP requests
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using var requestCts = CancellationTokenSource.CreateLinkedTokenSource(
                context.RequestAborted,
                timeoutCts.Token);
            var originalRequestAborted = context.RequestAborted;
            context.RequestAborted = requestCts.Token;
            try
            {
                logger.LogDebug("MCP request from client {ClientId}", clientId);
                await next(context);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                logger.LogWarning("MCP request timed out for client {ClientId}", clientId);
                if (!context.Response.HasStarted)
                {
                    context.Response.StatusCode = StatusCodes.Status504GatewayTimeout;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsJsonAsync(new
                    {
                        error = "request_timeout",
                        message = "The MCP request timed out after 30 seconds."
                    }, CancellationToken.None);
                }
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "Invalid JSON in MCP request from client {ClientId}", clientId);
                if (!context.Response.HasStarted)
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsJsonAsync(new
                    {
                        error = "invalid_json",
                        message = "The request body contains invalid JSON."
                    }, context.RequestAborted);
                }
            }
            catch (McpToolException ex)
            {
                logger.LogWarning("MCP tool error from client {ClientId}: {Code} - {Message}", clientId, ex.Code, ex.Message);
                if (!context.Response.HasStarted)
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsJsonAsync(new
                    {
                        error = ex.Code,
                        message = ex.Message
                    }, context.RequestAborted);
                }
            }
            finally
            {
                context.RequestAborted = originalRequestAborted;
            }
        }
    }
}
