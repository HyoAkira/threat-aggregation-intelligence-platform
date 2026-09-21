using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThreatIntelligence.API.Errors;
using ThreatIntelligence.Application.AI;
using ThreatIntelligence.Infrastructure.Persistence.RateLimiting;

namespace ThreatIntelligence.API.Controllers;

/// <summary>
/// Anonymous or authenticated on-demand IOC report generation and PDF export. Generation requests
/// contain only the IOC id in the route; the server resolves the evidence and report policy.
/// Pre-stream failures stay normal ProblemDetails responses; once streaming begins, failures are
/// safe <c>error</c> events. Nothing is persisted.
/// </summary>
[ApiController]
[Route("api/iocs/{id:int}/ai")]
[AllowAnonymous]
public sealed class IocAiController(
    AiReportGenerator reportGenerator,
    AiReportThrottle throttle,
    IMarkdownPdfRenderer pdfRenderer,
    ILogger<IocAiController> logger) : ControllerBase
{
    [HttpPost("reports")]
    public async Task<IActionResult> CreateReport(int id, CancellationToken cancellationToken)
    {
        var (scope, retryAfterSeconds) = await throttle.TryAcquireAsync(
            User?.Identity?.Name ?? "anonymous", cancellationToken);
        if (scope is null)
        {
            HttpContext.Response.Headers.RetryAfter = Math.Max(1, retryAfterSeconds).ToString();
            return ApiProblemFactory.Result(
                HttpContext,
                StatusCodes.Status429TooManyRequests,
                AiErrorCodes.QuotaExceeded,
                "Too many AI report requests",
                "The AI report limit has been reached. Try again shortly.");
        }

        using (scope)
        {
            var plan = await reportGenerator.PlanAsync(id, cancellationToken);
            if (!plan.Ok)
            {
                if (plan.Error == AiErrorCodes.NotConfigured &&
                    plan.ErrorMessage?.Contains("does not exist", StringComparison.Ordinal) == true)
                {
                    return ApiProblemFactory.Result(HttpContext, StatusCodes.Status404NotFound,
                        "ioc_not_found", "IOC not found", "The requested IOC does not exist.");
                }

                var status = plan.Error switch
                {
                    AiErrorCodes.Disabled => StatusCodes.Status409Conflict,
                    AiErrorCodes.NotConfigured => StatusCodes.Status409Conflict,
                    AiErrorCodes.InvalidConfiguration => StatusCodes.Status400BadRequest,
                    AiErrorCodes.CredentialUnavailable => StatusCodes.Status503ServiceUnavailable,
                    _ => StatusCodes.Status500InternalServerError,
                };
                return ApiProblemFactory.Result(
                    HttpContext, status, plan.Error!, "AI report unavailable", plan.ErrorMessage!);
            }

            logger.LogInformation(
                "AI report generation starting for IOC {IocId} by {User} with model {ModelId} and policy {PolicyVersion}",
                id, User?.Identity?.Name ?? "unknown", plan.ModelId, plan.Policy.Version);

            var correlationId = HttpContext.Items["CorrelationId"] as string ?? HttpContext.TraceIdentifier;
            Response.StatusCode = StatusCodes.Status200OK;
            Response.ContentType = "text/event-stream";
            Response.Headers.CacheControl = "no-store";
            Response.Headers["X-Accel-Buffering"] = "no";
            Response.Headers["X-Correlation-ID"] = correlationId;
            await Response.StartAsync(cancellationToken);

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var outcome = "complete";
            try
            {
                await foreach (var reportEvent in reportGenerator.RunAsync(plan, cancellationToken))
                {
                    await WriteEventAsync(reportEvent, cancellationToken);
                    if (reportEvent is AiErrorEvent) { outcome = "error"; break; }
                    if (reportEvent is AiCompleteEvent) { outcome = "complete"; break; }
                }
            }
            catch (AiUpstreamException)
            {
                outcome = "error";
                await WriteEventAsync(
                    new AiErrorEvent(AiErrorCodes.UpstreamFailure, "The model server request failed."),
                    cancellationToken);
            }
            catch (AiConfigurationException)
            {
                outcome = "error";
                await WriteEventAsync(
                    new AiErrorEvent(AiErrorCodes.InvalidConfiguration, "The AI configuration is not usable."),
                    cancellationToken);
            }
            catch (Exception) when (cancellationToken.IsCancellationRequested)
            {
                outcome = "aborted";
                throw;
            }
            catch (Exception)
            {
                outcome = "error";
                await WriteEventAsync(
                    new AiErrorEvent(AiErrorCodes.UpstreamFailure, "The report generation failed."),
                    cancellationToken);
            }

            logger.LogInformation(
                "AI report generation finished for IOC {IocId} with status {Status} in {ElapsedMs} ms",
                id, outcome, stopwatch.ElapsedMilliseconds);
        }

        return new EmptyResult();
    }

    [HttpPost("reports/pdf")]
    [RequestSizeLimit(128_000)]
    public async Task<IActionResult> CreateReportPdf(
        int id,
        [FromBody] PdfReportRequest? request,
        CancellationToken cancellationToken)
    {
        var (scope, retryAfterSeconds) = await throttle.TryAcquireAsync(
            User?.Identity?.Name ?? "anonymous", cancellationToken);
        if (scope is null)
        {
            HttpContext.Response.Headers.RetryAfter = Math.Max(1, retryAfterSeconds).ToString();
            return ApiProblemFactory.Result(
                HttpContext,
                StatusCodes.Status429TooManyRequests,
                AiErrorCodes.QuotaExceeded,
                "Too many AI report requests",
                "The AI report limit has been reached. Try again shortly.");
        }

        using (scope)
        {
            if (request is null || string.IsNullOrWhiteSpace(request.Report))
            {
                return ApiProblemFactory.Result(
                    HttpContext,
                    StatusCodes.Status400BadRequest,
                    ApiProblemFactory.ValidationCode,
                    "Invalid PDF request",
                    "A Markdown report is required.");
            }

            if (request.Report.Length > PdfReportLimits.MaxMarkdownCharacters)
            {
                return ApiProblemFactory.Result(
                    HttpContext,
                    StatusCodes.Status400BadRequest,
                    ApiProblemFactory.ValidationCode,
                    "Invalid PDF request",
                    "The Markdown report is too large to convert.");
            }

            var generatedAtUtc = DateTimeOffset.TryParse(
                request.GeneratedAtUtc,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var parsedGeneratedAtUtc)
                ? parsedGeneratedAtUtc
                : DateTimeOffset.UtcNow;

            try
            {
                var pdf = await pdfRenderer.RenderAsync(
                    request.Report,
                    new PdfReportMetadata(id, generatedAtUtc),
                    cancellationToken);
                Response.Headers.CacheControl = "no-store";
                return File(pdf, "application/pdf", $"ioc-report-{id}.pdf");
            }
            catch (PdfRenderingException exception)
            {
                logger.LogWarning(
                    "IOC report PDF conversion failed for {IocId} with code {Code}",
                    id,
                    exception.Code);
                var status = exception.Code is "pdf_input_invalid" or "pdf_input_too_large"
                    ? StatusCodes.Status400BadRequest
                    : StatusCodes.Status503ServiceUnavailable;
                return ApiProblemFactory.Result(
                    HttpContext,
                    status,
                    exception.Code,
                    "PDF export unavailable",
                    "The report could not be converted to PDF right now.");
            }
        }
    }

    private async Task WriteEventAsync(AiReportEvent reportEvent, CancellationToken cancellationToken)
    {
        var (name, payload) = reportEvent switch
        {
            AiMetaEvent meta => ("meta", JsonSerializer.Serialize(
                new { iocId = meta.IocId, modelId = meta.ModelId, policyVersion = meta.PolicyVersion, sourceCount = meta.SourceCount, omittedSourceCount = meta.OmittedSourceCount }, AiReportJson.Options)),
            AiDeltaEvent delta => ("delta", JsonSerializer.Serialize(
                new { text = delta.Text }, AiReportJson.Options)),
            AiCompleteEvent complete => ("complete", JsonSerializer.Serialize(
                new { report = complete.Report, modelId = complete.ModelId, policyVersion = complete.PolicyVersion, generatedAtUtc = complete.GeneratedAtUtc }, AiReportJson.Options)),
            AiErrorEvent error => ("error", JsonSerializer.Serialize(
                new { code = error.Code, message = error.Message }, AiReportJson.Options)),
            _ => throw new InvalidOperationException("Unknown report event."),
        };

        await Response.WriteAsync($"event: {name}\n", cancellationToken);
        await Response.WriteAsync($"data: {payload}\n\n", cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }
}

public sealed record PdfReportRequest(string? Report, string? GeneratedAtUtc);
