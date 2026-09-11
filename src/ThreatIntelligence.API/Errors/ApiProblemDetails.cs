using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;

namespace ThreatIntelligence.API.Errors;

/// <summary>
/// RFC 7807 problem detail extended with the stable machine-readable <c>code</c> and the request's
/// <c>correlationId</c> required by the API error contract.
/// </summary>
public sealed class ApiProblemDetails : ProblemDetails
{
    [JsonPropertyName("code")]
    public string Code { get; init; } = "error";

    [JsonPropertyName("correlationId")]
    public string? CorrelationId { get; init; }
}
