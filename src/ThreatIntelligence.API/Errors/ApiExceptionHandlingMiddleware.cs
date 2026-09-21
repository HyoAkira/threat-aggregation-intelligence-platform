using ThreatIntelligence.Application.Features.Iocs.Queries;

namespace ThreatIntelligence.API.Errors;

/// <summary>
/// Maps application and API exceptions to safe RFC 9457 problem details carrying the request's
/// correlation id. Unexpected exceptions become a generic 500 that never leaks stack traces or
/// internal state.
/// </summary>
public sealed class ApiExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ApiExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            if (context.Response.HasStarted)
            {
                throw;
            }

            var (status, code, title, detail) = Map(exception);

            if (status >= StatusCodes.Status500InternalServerError)
            {
                logger.LogError(
                    exception,
                    "Unhandled API error. CorrelationId={CorrelationId}",
                    context.Items["CorrelationId"]);
            }
            else
            {
                logger.LogInformation(
                    "Handled API failure {Code} ({Status}). CorrelationId={CorrelationId}",
                    code,
                    status,
                    context.Items["CorrelationId"]);
            }

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

    private static (int Status, string Code, string Title, string Detail) Map(Exception exception) =>
        exception switch
        {
            IocQueryValidationException validation => (
                StatusCodes.Status400BadRequest,
                validation.Code,
                "Invalid request",
                validation.Message),
            IocRelationNotSupportedException unsupported => (
                StatusCodes.Status400BadRequest,
                unsupported.Code,
                "Unsupported relation",
                unsupported.Message),
            IocNotFoundException notFound => (
                StatusCodes.Status404NotFound,
                notFound.Code,
                "Not found",
                notFound.Message),
            ApiConflictException conflict => (
                StatusCodes.Status409Conflict,
                conflict.Code,
                "Conflict",
                conflict.Message),
            _ => (
                StatusCodes.Status500InternalServerError,
                ApiProblemFactory.ServerErrorCode,
                "Unexpected error",
                "An unexpected error occurred while processing the request.")
        };
}
