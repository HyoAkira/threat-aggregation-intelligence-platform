using System.ComponentModel;
using System.Threading;
using ModelContextProtocol.Server;
using MediatR;
using Microsoft.Extensions.Logging;
using ThreatIntelligence.Application.Features.Iocs.Queries.BrowseIocs;
using ThreatIntelligence.Application.Features.Iocs.Queries.GetIocRelations;
using ThreatIntelligence.Application.Features.Iocs.Queries.SearchIocs;
using ThreatIntelligence.Application.Features.Iocs.Queries;
using ThreatIntelligence.Application.Abstractions.Persistence;

namespace ThreatIntelligence.API.Mcp;

[Description("Public IOC MCP server - read-only indicator investigation tools")]
[McpServerToolType]
public sealed partial class PublicIocMcpServer(
    ISender mediator,
    IIocQueryReader queryReader,
    ILogger<PublicIocMcpServer> logger)
{
    [McpServerTool(Name = "ioc_list_providers", ReadOnly = true)]
    [Description("Returns the list of all provider names that contribute IOC data.")]
    public async Task<IReadOnlyList<string>> ListProvidersAsync(CancellationToken cancellationToken)
    {
        logger.LogDebug("MCP tool ioc_list_providers invoked");
        var providers = await queryReader.GetProviderNamesAsync(cancellationToken);
        logger.LogDebug("MCP tool ioc_list_providers returned {Count} providers", providers.Count);
        return providers;
    }

    [McpServerTool(Name = "ioc_search_exact", ReadOnly = true)]
    [Description("Searches for IOC groups matching an exact value.")]
    public async Task<PagedIocGroups> SearchExactAsync(
        [Description("The IOC value to search for (IP address, domain, or URL)")] string query,
        [Description("Optional tag filter")] string? tag = null,
        [Description("Optional provider name filter")] string? provider = null,
        [Description("Optional malware filter (at most 200 characters)")] string? malware = null,
        [Description("Optional target filter (at most 500 characters)")] string? target = null,
        [Description("Page number (1-based)")] int page = 1,
        [Description("Number of items per page (1-100)")] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug("MCP tool ioc_search_exact invoked: query={Query}, page={Page}, pageSize={PageSize}", query, page, pageSize);
        if (string.IsNullOrWhiteSpace(query) || query.Length > 2048)
        {
            throw new McpToolException("invalid_input", "The IOC query must contain at most 2048 characters.");
        }
        ValidatePage(page, pageSize);

        var malwareFilter = NormalizeMetadataFilter(malware, IocSearchEvaluator.MaxMalwareFilterLength, "malware");
        var targetFilter = NormalizeMetadataFilter(target, IocSearchEvaluator.MaxTargetFilterLength, "target");

        var result = await mediator.Send(
            new SearchIocsQuery(query, tag, provider, page, pageSize, malwareFilter, targetFilter),
            cancellationToken);
        logger.LogDebug("MCP tool ioc_search_exact returned {Count} items", result.Items.Count);
        return MapPagedResult(result);
    }

    [McpServerTool(Name = "ioc_get_detail", ReadOnly = true)]
    [Description("Returns full details for an IOC group by its database id.")]
    public async Task<IocGroup> GetDetailAsync(
        [Description("The database id of the IOC group")] int id,
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug("MCP tool ioc_get_detail invoked: id={Id}", id);
        if (id <= 0)
        {
            throw new McpToolException("invalid_input", "The IOC id must be a positive integer.");
        }
        var group = await queryReader.GetDetailGroupAsync(id, cancellationToken);
        if (group is null)
        {
            throw new McpToolException("not_found", $"No IOC row exists with id {id}.");
        }
        logger.LogDebug("MCP tool ioc_get_detail returned group {Id}", id);
        return MapGroup(group);
    }

    [McpServerTool(Name = "ioc_browse", ReadOnly = true)]
    [Description("Browses IOC groups with optional filters and pagination.")]
    public async Task<PagedIocGroups> BrowseAsync(
        [Description("IOC type filter: ipv4, ipv6, domain, or url")] string? type = null,
        [Description("Provider name filter")] string? provider = null,
        [Description("Tag filter")] string? tag = null,
        [Description("Optional malware filter (at most 200 characters)")] string? malware = null,
        [Description("Optional target filter (at most 500 characters)")] string? target = null,
        [Description("Filter first seen from (ISO 8601 UTC)")] string? firstSeenFrom = null,
        [Description("Filter first seen to (ISO 8601 UTC)")] string? firstSeenTo = null,
        [Description("Filter last seen from (ISO 8601 UTC)")] string? lastSeenFrom = null,
        [Description("Filter last seen to (ISO 8601 UTC)")] string? lastSeenTo = null,
        [Description("Sort field")] string? orderBy = null,
        [Description("Sort direction: asc or desc")] string? dir = null,
        [Description("Page number (1-based)")] int page = 1,
        [Description("Number of items per page (1-100)")] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug("MCP tool ioc_browse invoked: page={Page}, pageSize={PageSize}", page, pageSize);
        ValidatePage(page, pageSize);

        var malwareFilter = NormalizeMetadataFilter(malware, IocSearchEvaluator.MaxMalwareFilterLength, "malware");
        var targetFilter = NormalizeMetadataFilter(target, IocSearchEvaluator.MaxTargetFilterLength, "target");

        var result = await mediator.Send(new BrowseIocsQuery(
            type, provider, tag,
            ParseDateTime(firstSeenFrom), ParseDateTime(firstSeenTo),
            ParseDateTime(lastSeenFrom), ParseDateTime(lastSeenTo),
            orderBy, dir, page, pageSize, malwareFilter, targetFilter), cancellationToken);
        logger.LogDebug("MCP tool ioc_browse returned {Count} items", result.Items.Count);
        return MapPagedResult(result);
    }

    [McpServerTool(Name = "ioc_get_relations", ReadOnly = true)]
    [Description("Returns related IOC groups for a given IOC by host or metadata relationship.")]
    public async Task<PagedIocGroups> GetRelationsAsync(
        [Description("The database id of the IOC group")] int id,
        [Description("Relation type: urlHost, parentDomain, malware, or target")] string relation,
        [Description("Page number (1-based)")] int page = 1,
        [Description("Number of items per page (1-100)")] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug("MCP tool ioc_get_relations invoked: id={Id}, relation={Relation}", id, relation);
        if (id <= 0)
        {
            throw new McpToolException("invalid_input", "The IOC id must be a positive integer.");
        }
        ValidatePage(page, pageSize);
        var relationKind = ParseRelationKind(relation);
        var result = await mediator.Send(new GetIocRelationsQuery(id, relationKind.ToString(), page, pageSize), cancellationToken);
        if (result is null)
        {
            throw new McpToolException("not_found", $"No IOC row exists with id {id}.");
        }
        logger.LogDebug("MCP tool ioc_get_relations returned {Count} items", result.Items.Count);
        return MapPagedResult(result);
    }

    private static IocRelationKind ParseRelationKind(string relation) =>
        relation.ToLowerInvariant() switch
        {
            "urlhost" => IocRelationKind.UrlHost,
            "parentdomain" => IocRelationKind.ParentDomain,
            "malware" => IocRelationKind.Malware,
            "target" => IocRelationKind.Target,
            _ => throw new McpToolException("invalid_input", "The relation must be urlHost, parentDomain, malware, or target.")
        };

    private static string? NormalizeMetadataFilter(string? value, int maxLength, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new McpToolException(
                "invalid_input",
                $"The {name} filter must be no longer than {maxLength} characters.");
        }

        return trimmed;
    }

    private static DateTime? ParseDateTime(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null :
        DateTime.TryParse(value, null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed
            : throw new McpToolException("invalid_input", "The date value is not a valid ISO 8601 UTC datetime.");

    private static void ValidatePage(int page, int pageSize)
    {
        if (page < 1 || pageSize is < 1 or > 100)
            throw new McpToolException("invalid_input", "Page must be at least 1 and pageSize must be between 1 and 100.");
    }

    private static PagedIocGroups MapPagedResult(PagedResult<IocGroupResult> result) =>
        new(
            result.Items.Select(MapGroup).ToList(),
            result.Page,
            result.PageSize,
            result.TotalCount,
            result.TotalCount > (long)result.Page * result.PageSize,
            Math.Max(0, result.TotalCount - (long)result.Page * result.PageSize));

    private static IocGroup MapGroup(IocGroupResult group) =>
        new(group.Id, group.IocValue, group.IocType.ToString().ToLowerInvariant(),
            group.UrlHost, group.ParentDomain,
            group.FirstSeenUtc.ToString("o"), group.LastSeenUtc?.ToString("o"),
            group.Sources.Select(MapSource).ToList());

    private static IocSource MapSource(IocSourceResult source) =>
        new(source.Provider, source.ProviderRecordId, source.ProviderDetailsLink, source.Port,
            source.FirstSeenUtc.ToString("o"), source.LastSeenUtc?.ToString("o"),
            source.ThreatType, source.Malware, source.MalwareAlias, source.MalwarePrintable,
            source.IsCompromised, source.Reporter, source.Anonymous, source.IsVerified, source.Target,
            source.Tags.ToList(), source.IsFalsePositive);
}

public sealed class McpToolException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public sealed record PagedIocGroups(
    IReadOnlyList<IocGroup> items,
    int page,
    int pageSize,
    int totalCount,
    bool isTruncated,
    long omittedCount);
public sealed record IocGroup(int id, string iocValue, string iocType, string? urlHost, string? parentDomain,
    string firstSeenUtc, string? lastSeenUtc, IReadOnlyList<IocSource> sources);
public sealed record IocSource(string provider, string providerRecordId, string? providerDetailsLink, int? port,
    string firstSeenUtc, string? lastSeenUtc, string? threatType, string? malware, string? malwareAlias,
    string? malwarePrintable, bool? isCompromised, string? reporter, bool? anonymous, bool? isVerified,
    string? target, IReadOnlyList<string> tags, bool isFalsePositive);
