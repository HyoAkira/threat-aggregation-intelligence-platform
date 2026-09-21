using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThreatIntelligence.API.Errors;
using ThreatIntelligence.Domain.Entities;
using ThreatIntelligence.Infrastructure.Persistence;

namespace ThreatIntelligence.API.Controllers;

[ApiController]
[Route("api/admin/ai/report-policy")]
public sealed class AdminAiReportPolicyController(ThreatIntelligenceDbContext dbContext, TimeProvider timeProvider)
    : ControllerBase
{
    private const int MaxTextLength = 20000;

    [HttpGet]
    public async Task<ActionResult<AiReportPolicyResponse>> Get(CancellationToken cancellationToken)
    {
        var versions = await dbContext.AiReportPolicyVersions.AsNoTracking()
            .OrderBy(version => version.Version)
            .ToListAsync(cancellationToken);
        if (versions.Count == 0)
        {
            return ApiProblemFactory.Result(
                HttpContext,
                StatusCodes.Status404NotFound,
                "report_policy_missing",
                "Report policy missing",
                "No report instruction version has been published yet.");
        }

        var activeVersion = (await ReadActiveVersionAsync(cancellationToken)) ?? versions.Max(version => version.Version);
        var active = versions.Single(version => version.Version == activeVersion);
        return Ok(new AiReportPolicyResponse(
            active.Version,
            active.Text,
            active.AuthorUserId,
            active.PublishedAt,
            versions.Select(version => new AiReportPolicyVersionResponse(
                version.Version,
                version.Text,
                version.AuthorUserId,
                version.PublishedAt)).ToList()));
    }

    [HttpPut]
    public async Task<ActionResult<AiReportPolicyResponse>> Put(
        AiReportPolicyPublishRequest request,
        CancellationToken cancellationToken)
    {
        var text = request.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return Invalid("The report instruction text is required.");
        }

        if (text.Length > MaxTextLength)
        {
            return Invalid($"The report instruction text must be at most {MaxTextLength} characters.");
        }

        var activeVersion = await ReadActiveVersionAsync(cancellationToken);
        if (request.ExpectedVersion is not null && request.ExpectedVersion.Value != activeVersion)
        {
            return ApiProblemFactory.Result(
                HttpContext,
                StatusCodes.Status409Conflict,
                "report_policy_conflict",
                "Report policy conflict",
                "The report policy was changed by another administrator; reload and reapply the edit.");
        }

        var nextVersion = await dbContext.AiReportPolicyVersions.AsNoTracking()
            .MaxAsync(version => (int?)version.Version, cancellationToken) ?? 0;
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var author = User?.FindFirstValue(ClaimTypes.Name);
        var created = new AiReportPolicyVersion(nextVersion + 1, text, author, now);
        dbContext.AiReportPolicyVersions.Add(created);

        var pointer = await dbContext.AiReportPolicyActive.SingleOrDefaultAsync(cancellationToken);
        if (pointer is null)
        {
            dbContext.AiReportPolicyActive.Add(new AiReportPolicyActive(created.Version, now));
        }
        else
        {
            pointer.Update(created.Version, now);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new AiReportPolicyResponse(
            created.Version,
            created.Text,
            created.AuthorUserId,
            created.PublishedAt,
            []));
    }

    private async Task<int?> ReadActiveVersionAsync(CancellationToken cancellationToken) =>
        await dbContext.AiReportPolicyActive.AsNoTracking()
            .Select(pointer => (int?)pointer.ActiveVersion)
            .SingleOrDefaultAsync(cancellationToken);

    private ActionResult Invalid(string detail) => ApiProblemFactory.Result(
        HttpContext,
        StatusCodes.Status400BadRequest,
        "invalid_report_policy",
        "Invalid report policy",
        detail);
}

public sealed record AiReportPolicyPublishRequest(string? Text, int? ExpectedVersion);
public sealed record AiReportPolicyVersionResponse(int Version, string Text, string? AuthorUserId, DateTime PublishedAt);
public sealed record AiReportPolicyResponse(
    int ActiveVersion,
    string Text,
    string? AuthorUserId,
    DateTime PublishedAt,
    IReadOnlyList<AiReportPolicyVersionResponse> History);
