namespace ThreatIntelligence.Application.BatchWriter;

/// <summary>
/// Base failure raised by the atomic batch writer. The whole batch must roll back.
/// </summary>
public class IocBatchWriteException(string message, Exception? innerException = null)
    : Exception(message, innerException);

/// <summary>
/// Raised when an incoming record's provider-native identity maps to a row whose normalized
/// identity already belongs to a different provider row. Aborts the entire fetch.
/// </summary>
public sealed class IocIdentityConflictException(string message)
    : IocBatchWriteException(message);