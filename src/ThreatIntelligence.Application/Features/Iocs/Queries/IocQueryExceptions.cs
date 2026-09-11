namespace ThreatIntelligence.Application.Features.Iocs.Queries;

/// <summary>
/// Stable machine-readable codes carried on the API problem details for IOC query failures.
/// </summary>
public static class IocErrorCodes
{
    public const string InvalidInput = "invalid_input";
    public const string NotFound = "ioc_not_found";
    public const string RelationNotSupported = "ioc_relation_not_supported";
}

/// <summary>
/// Invalid, unclassifiable, or out-of-range IOC query input. Maps to a 400 ProblemDetails whose
/// <c>code</c> is <see cref="Code"/> and whose detail names the offending input safely.
/// </summary>
public sealed class IocQueryValidationException(string code, string detail)
    : Exception(detail)
{
    public string Code { get; } = code;
}

/// <summary>
/// A valid query or relation request that matched no IOC group. Maps to a 404 ProblemDetails.
/// </summary>
public sealed class IocNotFoundException(string code, string detail)
    : Exception(detail)
{
    public string Code { get; } = code;
}

/// <summary>
/// A relation request that the resolved group cannot support (for example an IP group, which stores
/// no host columns). Maps to a 400 ProblemDetails.
/// </summary>
public sealed class IocRelationNotSupportedException(string code, string detail)
    : Exception(detail)
{
    public string Code { get; } = code;
}
