using ThreatIntelligence.Domain.Enums;

namespace ThreatIntelligence.Application.Features.Iocs.Queries;

/// <summary>
/// Wire token for <see cref="IocType"/> used by the query response contract and the browse
/// <c>type</c> filter. The spec fixes the lowercase tokens <c>ipv4</c>, <c>ipv6</c>, <c>domain</c>,
/// and <c>url</c>; they are independent of the persisted ordinal values.
/// </summary>
public static class IocTypeToken
{
    public const string Ipv4 = "ipv4";
    public const string Ipv6 = "ipv6";
    public const string Domain = "domain";
    public const string Url = "url";

    public static string ToToken(IocType type) => type switch
    {
        IocType.Ipv4 => Ipv4,
        IocType.Ipv6 => Ipv6,
        IocType.Domain => Domain,
        IocType.Url => Url,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown IOC type.")
    };

    public static bool TryParse(string? token, out IocType type)
    {
        switch (token?.Trim().ToLowerInvariant())
        {
            case Ipv4:
                type = IocType.Ipv4;
                return true;
            case Ipv6:
                type = IocType.Ipv6;
                return true;
            case Domain:
                type = IocType.Domain;
                return true;
            case Url:
                type = IocType.Url;
                return true;
            default:
                type = default;
                return false;
        }
    }
}

/// <summary>
/// One normalized IOC group as projected onto the wire by every query endpoint. The group exposes
/// the id of one of its stored rows so callers can link to detail and relations, the normalized
/// value, its lowercase type token, the nullable relations, the aggregate first/last seen across
/// its returned sources, and one source entry per matching provider row.
/// </summary>
public sealed record IocGroupResult(
    int Id,
    string IocValue,
    string IocType,
    string? UrlHost,
    string? ParentDomain,
    DateTime FirstSeenUtc,
    DateTime? LastSeenUtc,
    IReadOnlyList<IocSourceResult> Sources);
