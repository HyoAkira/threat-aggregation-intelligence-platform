using MediatR;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThreatIntelligence.Application.Features.Iocs.Queries;
using ThreatIntelligence.Application.Features.Iocs.Queries.BrowseIocs;
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
        [FromQuery] string? q,
        [FromQuery, Range(1, 100)] int limit,
        CancellationToken cancellationToken) =>
        Ok(await queryReader.GetTagCatalogAsync(q, limit, cancellationToken));

    [HttpGet("search")]
    public async Task<ActionResult<PagedResult<IocGroupResult>>> Search(
        [FromQuery] string? q,
        [FromQuery] string? tag,
        [FromQuery] string? provider,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new SearchIocsQuery(q ?? string.Empty, tag, provider, page, pageSize),
            cancellationToken);
        return Ok(result);
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
                pageSize),
            cancellationToken);
        return Ok(result);
    }
}
