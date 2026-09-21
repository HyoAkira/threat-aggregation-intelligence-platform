using Microsoft.AspNetCore.Mvc;
using ThreatIntelligence.API.Errors;
using ThreatIntelligence.Application.Abstractions.Secrets;

namespace ThreatIntelligence.API.Controllers;

[ApiController]
[Route("api/admin/credentials")]
public sealed class AdminCredentialsController(ISecretStore secretStore) : ControllerBase
{
    [HttpGet("{identity}")]
    public async Task<ActionResult<CredentialStatusResponse>> Get(string identity, CancellationToken cancellationToken)
    {
        if (!ManagedSecretNames.IsSupported(identity)) return InvalidIdentity();
        var status = await secretStore.GetStatusAsync(identity, cancellationToken);
        return Ok(ToResponse(identity, status));
    }

    [HttpPut("{identity}")]
    public async Task<ActionResult<CredentialStatusResponse>> Put(
        string identity,
        CredentialWriteRequest request,
        CancellationToken cancellationToken)
    {
        if (!ManagedSecretNames.IsSupported(identity)) return InvalidIdentity();
        if (string.IsNullOrWhiteSpace(request.Value) || request.Value.Length > 4096)
        {
            return ApiProblemFactory.Result(HttpContext, StatusCodes.Status400BadRequest,
                "invalid_credential", "Invalid credential", "The credential is blank or too long.");
        }

        var result = await secretStore.SetAsync(identity, request.Value, cancellationToken);
        return result.Status switch
        {
            SecretMutationStatus.Success => Ok(ToResponse(identity, result.StatusMetadata)),
            SecretMutationStatus.InvalidIdentity => InvalidIdentity(),
            SecretMutationStatus.InvalidValue => ApiProblemFactory.Result(HttpContext, StatusCodes.Status400BadRequest,
                "invalid_credential", "Invalid credential", "The credential is blank or too long."),
            _ => ApiProblemFactory.Result(HttpContext, StatusCodes.Status503ServiceUnavailable,
                "credential_store_unavailable", "Credential store unavailable",
                "Credential storage is not configured for this host.")
        };
    }

    [HttpDelete("{identity}")]
    public async Task<IActionResult> Delete(string identity, CancellationToken cancellationToken)
    {
        if (!ManagedSecretNames.IsSupported(identity)) return InvalidIdentity();
        await secretStore.ClearAsync(identity, cancellationToken);
        return NoContent();
    }

    private static CredentialStatusResponse ToResponse(string identity, ManagedSecretStatus? status) =>
        new(identity, status is not null, status?.KeyVersion, status?.UpdatedAt);

    private ActionResult InvalidIdentity() => ApiProblemFactory.Result(HttpContext, StatusCodes.Status400BadRequest,
        "unsupported_credential", "Unsupported credential", "The credential identity is not supported.");
}

public sealed record CredentialWriteRequest(string Value);

public sealed record CredentialStatusResponse(string Identity, bool Configured, int? KeyVersion, DateTime? UpdatedAt);
