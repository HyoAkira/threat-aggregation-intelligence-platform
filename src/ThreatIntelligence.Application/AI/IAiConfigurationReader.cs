namespace ThreatIntelligence.Application.AI;

/// <summary>
/// Read model of the persisted non-secret AI connection settings plus whether the server API key
/// credential is configured. Consumers never receive key material.
/// </summary>
public sealed record AiConnectionSnapshot(
    string? BaseUrl,
    string? ModelId,
    bool Enabled,
    bool ApiKeyConfigured);

/// <summary>Active report-policy version pinned at generation start.</summary>
public sealed record AiReportPolicySnapshot(int Version, string Text);

public interface IAiConfigurationReader
{
    /// <summary>Returns the persisted connection settings, or null when no settings row exists.</summary>
    Task<AiConnectionSnapshot?> GetConnectionAsync(CancellationToken cancellationToken);

    /// <summary>Returns the active report-policy version, or null when no policy has been seeded.</summary>
    Task<AiReportPolicySnapshot?> GetActivePolicyAsync(CancellationToken cancellationToken);
}
