using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace ThreatIntelligence.API.Middleware;

public class ProblemDetailsExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (statusCode, title) = MapException(exception);

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = statusCode == StatusCodes.Status500InternalServerError
                ? "An unexpected error occurred. Please try again later."
                : exception.Message,
            Instance = httpContext.Request.Path
        };

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/problem+json";
        await httpContext.Response.WriteAsync(JsonSerializer.Serialize(problemDetails), cancellationToken);

        return true;
    }

    private static (int StatusCode, string Title) MapException(Exception exception) => exception switch
    {
        InvalidOperationException ex when ex.Message.Contains("not configured") =>
            (StatusCodes.Status401Unauthorized, "Configuration missing"),
        HttpRequestException =>
            (StatusCodes.Status502BadGateway, "Upstream service error"),
        InvalidDataException =>
            (StatusCodes.Status422UnprocessableEntity, "Invalid data"),
        _ =>
            (StatusCodes.Status500InternalServerError, "Internal server error"),
    };
}