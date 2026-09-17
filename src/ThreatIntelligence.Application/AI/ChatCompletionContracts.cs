namespace ThreatIntelligence.Application.AI;

/// <summary>One incremental chunk of assistant text from the chat-completions stream.</summary>
public sealed record ChatCompletionDelta(string Text);

public sealed record ChatCompletionMessage(string Role, string Content);

public sealed record ChatCompletionRequest(
    string BaseUrl,
    string ModelId,
    IReadOnlyList<ChatCompletionMessage> Messages,
    double Temperature,
    int MaxTokens);

public interface IChatCompletionClient
{
    /// <summary>
    /// Streams assistant deltas from the configured OpenAI-compatible chat-completions endpoint.
    /// Throws <see cref="AiConfigurationException"/> when the connection is unusable and
    /// <see cref="AiUpstreamException"/> when the upstream call fails.
    /// </summary>
    IAsyncEnumerable<ChatCompletionDelta> StreamAsync(
        ChatCompletionRequest request,
        CancellationToken cancellationToken);
}

/// <summary>Stable, non-secret failure codes surfaced to the API error contract.</summary>
public static class AiErrorCodes
{
    public const string Disabled = "ai_disabled";
    public const string NotConfigured = "ai_not_configured";
    public const string InvalidConfiguration = "ai_invalid_configuration";
    public const string CredentialUnavailable = "ai_credential_unavailable";
    public const string UpstreamFailure = "ai_upstream_failure";
    public const string UpstreamTimeout = "ai_upstream_timeout";
    public const string UnsupportedUpstreamResponse = "ai_unsupported_upstream_response";
    public const string OutputInvalid = "ai_output_invalid";
    public const string QuotaExceeded = "ai_quota_exceeded";
}

public sealed class AiConfigurationException(string code, string message)
    : Exception(message)
{
    public string Code { get; } = code;
}

public sealed class AiUpstreamException(string code, string message, int? upstreamStatus = null)
    : Exception(message)
{
    public string Code { get; } = code;
    public int? UpstreamStatus { get; } = upstreamStatus;
}

public sealed class AiOutputInvalidException(string detail)
    : Exception(detail)
{
    public string Detail { get; } = detail;
}
