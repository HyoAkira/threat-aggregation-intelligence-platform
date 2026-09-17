namespace ThreatIntelligence.Application.Abstractions.Secrets;

public interface ISecretStore
{
    Task<ManagedSecretStatus?> GetStatusAsync(string name, CancellationToken cancellationToken);

    Task<string?> ResolveAsync(string name, CancellationToken cancellationToken);

    Task<SecretMutationResult> SetAsync(string name, string value, CancellationToken cancellationToken);

    Task<bool> ClearAsync(string name, CancellationToken cancellationToken);
}

public sealed record ManagedSecretStatus(
    string Name,
    bool IsConfigured,
    int? KeyVersion,
    DateTime? UpdatedAt);

public enum SecretMutationStatus
{
    Success,
    InvalidIdentity,
    InvalidValue,
    Unavailable
}

public sealed record SecretMutationResult(SecretMutationStatus Status, ManagedSecretStatus? StatusMetadata);

public sealed class SecretManagementUnavailableException(string message, Exception? innerException = null)
    : Exception(message, innerException);

public sealed class SecretResolutionException(string message, Exception? innerException = null)
    : Exception(message, innerException);
