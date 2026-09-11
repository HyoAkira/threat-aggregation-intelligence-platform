namespace ThreatIntelligence.Application.Features.Iocs.Queries;

/// <summary>
/// Shared envelope for every multi-item query endpoint. <see cref="TotalCount"/> is the number of
/// distinct result groups matching the request, independent of the requested page.
/// </summary>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    public static PagedResult<T> Empty(int page, int pageSize) => new([], page, pageSize, 0);
}
