using Microsoft.AspNetCore.Mvc;
using ThreatIntelligence.API.Serialization;

namespace ThreatIntelligence.API.Errors;

public static class ApiProblemFactory
{
    public const string ProblemTypePrefix = "urn:threat-intelligence:problem:";
    public const string ProblemMediaType = "application/problem+json";
    public const string ValidationCode = "validation_error";
    public const string UnauthenticatedCode = "unauthenticated";
    public const string ForbiddenCode = "forbidden";
    public const string ConflictCode = "conflict";
    public const string ServerErrorCode = "server_error";

    public static ApiProblemDetails Create(
        HttpContext? context,
        int status,
        string code,
        string title,
        string detail)
    {
        var correlationId = context?.Items?["CorrelationId"] as string;
        if (correlationId is null)
        {
            correlationId = context?.TraceIdentifier ?? "no-context";
        }
        return new ApiProblemDetails
        {
            Type = $"{ProblemTypePrefix}{code}",
            Title = title,
            Status = status,
            Detail = detail,
            Code = code,
            CorrelationId = correlationId
        };
    }

    public static ObjectResult Result(
        HttpContext? context,
        int status,
        string code,
        string title,
        string detail)
    {
        var problem = Create(context, status, code, title, detail);
        ObjectResult result = status switch
        {
            StatusCodes.Status400BadRequest => new BadRequestObjectResult(problem),
            StatusCodes.Status401Unauthorized => new UnauthorizedObjectResult(problem),
            StatusCodes.Status404NotFound => new NotFoundObjectResult(problem),
            StatusCodes.Status409Conflict => new ConflictObjectResult(problem),
            _ => new ObjectResult(problem) { StatusCode = status }
        };
        result.ContentTypes = [ProblemMediaType];
        return result;
    }

    public static Task WriteAsync(
        HttpResponse response,
        ApiProblemDetails problem,
        CancellationToken cancellationToken = default) =>
        response.WriteAsJsonAsync(problem, ApiJson.Create(), ProblemMediaType, cancellationToken);
}
