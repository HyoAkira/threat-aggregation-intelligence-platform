using MediatR;

namespace ThreatIntelligence.Application.Features.Iocs.Queries.BulkSearchIocs;

/// <summary>
/// Bounded multi-value IOC search. <see cref="Queries"/> is the ordered list of submitted lines;
/// the handler removes blank entries, trims the rest, and returns one outcome per effective input.
/// </summary>
public sealed record BulkSearchIocsQuery(
    IReadOnlyList<string>? Queries,
    string? Tag = null,
    string? Provider = null,
    int? Page = null,
    int? PageSize = null) : IRequest<BulkSearchIocsResult>;

/// <summary>Stable per-item status tokens for the bulk response.</summary>
public static class BulkSearchStatus
{
    public const string Matched = "matched";
    public const string NotFound = "not_found";
    public const string Invalid = "invalid";
}

/// <summary>Bounds shared by the bulk request contract and its handler.</summary>
public static class BulkSearchLimits
{
    public const int MaxQueries = 100;
    public const int MaxQueryLength = 2048;
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
}

/// <summary>A safe per-item failure code and detail; never carries secrets beyond the input.</summary>
public sealed record BulkSearchProblem(string Code, string Detail);

/// <summary>
/// One ordered outcome for a submitted non-empty line. A <c>matched</c> item carries the page of
/// existing <see cref="IocGroupResult"/> groups and the shared paging metadata; <c>not_found</c> and
/// <c>invalid</c> items carry empty groups and a <see cref="BulkSearchProblem"/>.
/// </summary>
public sealed record BulkSearchIocItem(
    string Input,
    string Status,
    IReadOnlyList<IocGroupResult> Groups,
    int Page,
    int PageSize,
    int TotalCount,
    BulkSearchProblem? Error);

/// <summary>Ordered bulk outcomes plus summary counts that sum to <see cref="TotalCount"/>.</summary>
public sealed record BulkSearchIocsResult(
    IReadOnlyList<BulkSearchIocItem> Items,
    int TotalCount,
    int MatchedCount,
    int NotFoundCount,
    int InvalidCount);
