namespace ThreatIntelligence.API.Errors;

public static class ApiProblemFactory
{
    public const string ValidationCode = "validation_error";
    public const string UnauthenticatedCode = "unauthenticated";
    public const string ForbiddenCode = "forbidden";
    public const string ConflictCode = "conflict";
    public const string ServerErrorCode = "server_error";

    public static ApiProblemDetails Create(
        HttpContext context,
        int status,
        string code,
        string title,
        string detail)
    {
        var correlationId = context.Items["CorrelationId"] as string ?? context.TraceIdentifier;
        return new ApiProblemDetails
        {
            Type = $"https://httpstatuses.io/{status}",
            Title = title,
            Status = status,
            Detail = detail,
            Code = code,
            CorrelationId = correlationId
        };
    }
}
