using MediatR;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThreatIntelligence.Application.Features.Iocs.Queries;
using ThreatIntelligence.Application.Features.Iocs.Queries.BrowseIocs;
using ThreatIntelligence.Application.Features.Iocs.Queries.BulkSearchIocs;
using ThreatIntelligence.Application.Features.Iocs.Queries.GetIocDetail;
using ThreatIntelligence.Application.Features.Iocs.Queries.GetIocRelations;
using ThreatIntelligence.Application.Features.Iocs.Queries.SearchIocs;
using ThreatIntelligence.Application.Abstractions.Persistence;

namespace ThreatIntelligence.API.Controllers;

/// <summary>
/// Anonymous IOC query surface: combined search, detail by row id, opt-in paginated relations,
/// and filtered grouped browse. Admin-only concerns live under <c>/api/admin/*</c>.
/// </summary>
[ApiController]
[Route("api/iocs")]
[AllowAnonymous]
public sealed class IocsController(ISender sender, IIocQueryReader queryReader) : ControllerBase
{
    [HttpGet("providers")]
    public async Task<ActionResult<IReadOnlyList<string>>> Providers(
        CancellationToken cancellationToken) =>
        Ok(await queryReader.GetProviderNamesAsync(cancellationToken));

    [HttpGet("tags")]
    public async Task<ActionResult<IReadOnlyList<IocTagCatalogItem>>> Tags(
        [FromQuery, StringLength(100)] string? q,
        [FromQuery, Range(1, 100)] int? limit,
        CancellationToken cancellationToken) =>
        Ok(await queryReader.GetTagCatalogAsync(q, limit ?? 8, cancellationToken));

    [HttpGet("malware")]
    public async Task<ActionResult<IReadOnlyList<IocTagCatalogItem>>> Malware(
        [FromQuery, StringLength(200)] string? q,
        [FromQuery, Range(1, 100)] int? limit,
        CancellationToken cancellationToken) =>
        Ok(await queryReader.GetMetadataCatalogAsync(IocRelationKind.Malware, q, limit ?? 8, cancellationToken));

    [HttpGet("targets")]
    public async Task<ActionResult<IReadOnlyList<IocTagCatalogItem>>> Targets(
        [FromQuery, StringLength(500)] string? q,
        [FromQuery, Range(1, 100)] int? limit,
        CancellationToken cancellationToken) =>
        Ok(await queryReader.GetMetadataCatalogAsync(IocRelationKind.Target, q, limit ?? 8, cancellationToken));

    [HttpGet("search")]
    public async Task<ActionResult<PagedResult<IocGroupResult>>> Search(
        [FromQuery] string? q,
        [FromQuery] string? tag,
        [FromQuery] string? provider,
        [FromQuery] string? malware,
        [FromQuery] string? target,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new SearchIocsQuery(q ?? string.Empty, tag, provider, page, pageSize, malware, target),
            cancellationToken);
        return Ok(result);
    }

    [HttpPost("search/bulk")]
    public async Task<ActionResult<BulkSearchIocsResult>> BulkSearch(
        [FromBody] BulkSearchIocsQuery request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("suggestions")]
    public async Task<ActionResult<IReadOnlyList<IocSuggestionItem>>> Suggestions(
        [FromQuery] string? q,
        [FromQuery, Range(IocSuggestionLimits.MinLimit, IocSuggestionLimits.MaxLimit)] int? limit,
        CancellationToken cancellationToken)
    {
        var prefix = q?.Trim() ?? string.Empty;
        if (prefix.Length == 0)
        {
            throw new IocQueryValidationException(
                IocErrorCodes.InvalidInput,
                "The q parameter must be a non-empty prefix.");
        }

        if (prefix.Length > IocSuggestionLimits.MaxPrefixLength)
        {
            throw new IocQueryValidationException(
                IocErrorCodes.InvalidInput,
                $"The q parameter must be no longer than {IocSuggestionLimits.MaxPrefixLength} characters.");
        }

        var resolvedLimit = limit ?? IocSuggestionLimits.DefaultLimit;
        return Ok(await queryReader.GetIocSuggestionsAsync(prefix, resolvedLimit, cancellationToken));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<IocGroupResult>> Detail(int id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetIocDetailQuery(id), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id}/relations")]
    public async Task<ActionResult<PagedResult<IocGroupResult>>> Relations(
        int id,
        [FromQuery] string? relation,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetIocRelationsQuery(id, relation, page, pageSize),
            cancellationToken);
        return Ok(result);
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<IocGroupResult>>> Browse(
        [FromQuery] string? type,
        [FromQuery] string? provider,
        [FromQuery] string? tag,
        [FromQuery] string? malware,
        [FromQuery] string? target,
        [FromQuery] DateTime? firstSeenFrom,
        [FromQuery] DateTime? firstSeenTo,
        [FromQuery] DateTime? lastSeenFrom,
        [FromQuery] DateTime? lastSeenTo,
        [FromQuery] string? orderBy,
        [FromQuery] string? dir,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new BrowseIocsQuery(
                type,
                provider,
                tag,
                firstSeenFrom,
                firstSeenTo,
                lastSeenFrom,
                lastSeenTo,
                orderBy,
                dir,
                page,
                pageSize,
                malware,
                target),
            cancellationToken);
        return Ok(result);
    }
}
