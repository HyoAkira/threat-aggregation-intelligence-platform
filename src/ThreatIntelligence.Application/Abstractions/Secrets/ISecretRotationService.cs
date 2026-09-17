namespace ThreatIntelligence.Application.Abstractions.Secrets;

public interface ISecretRotationService
{
    Task<SecretRotationResult> ReencryptAsync(CancellationToken cancellationToken);
}

public sealed record SecretRotationResult(int Reencrypted, int RemainingOldVersions, bool IsAvailable);
