namespace ThreatIntelligence.API.Errors;

/// <summary>
/// A guarded conflict raised by API endpoints (for example a fetch already running). Maps to a 409
/// problem detail.
/// </summary>
public sealed class ApiConflictException(string code, string detail) : Exception(detail)
{
    public string Code { get; } = code;
}
