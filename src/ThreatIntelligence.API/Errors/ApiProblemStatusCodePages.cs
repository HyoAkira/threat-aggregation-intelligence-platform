using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Diagnostics;

namespace ThreatIntelligence.API.Errors;

public static class ApiProblemStatusCodePages
{
    public static async Task HandleAsync(StatusCodeContext statusContext)
    {
        var context = statusContext.HttpContext;
        if (!context.Request.Path.StartsWithSegments("/api") || context.Response.HasStarted)
        {
            return;
        }

        var status = context.Response.StatusCode;
        var (code, title, detail) = status switch
        {
            StatusCodes.Status400BadRequest => (
                "bad_request",
                "Bad request",
                "The request could not be understood."),
            StatusCodes.Status401Unauthorized => (
                ApiProblemFactory.UnauthenticatedCode,
                "Unauthorized",
                "Authentication is required to access this resource."),
            StatusCodes.Status403Forbidden => (
                ApiProblemFactory.ForbiddenCode,
                "Forbidden",
                "You do not have permission to perform this action."),
            StatusCodes.Status404NotFound => (
                "route_not_found",
                "Route not found",
                "The requested API route could not be found."),
            StatusCodes.Status405MethodNotAllowed => (
                "method_not_allowed",
                "Method not allowed",
                "The HTTP method is not supported for this route."),
            _ when status >= StatusCodes.Status500InternalServerError => (
                ApiProblemFactory.ServerErrorCode,
                "Unexpected error",
                "An unexpected error occurred while processing the request."),
            _ => (
                "request_failed",
                "Request failed",
                "The request could not be completed.")
        };

        var correlationId = context.Items["CorrelationId"] as string;
        context.Response.Clear();
        context.Response.StatusCode = status;
        if (correlationId is not null)
        {
            context.Response.Headers["X-Correlation-ID"] = correlationId;
        }

        await ApiProblemFactory.WriteAsync(
            context.Response,
            ApiProblemFactory.Create(context, status, code, title, detail),
            context.RequestAborted);
    }
}
