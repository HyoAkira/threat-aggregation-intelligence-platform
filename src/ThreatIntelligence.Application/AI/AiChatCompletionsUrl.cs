namespace ThreatIntelligence.Application.AI;

/// <summary>
/// Normalizes an Admin-configured OpenAI-compatible Base URL and appends the fixed
/// chat-completions operation path. Applies no outbound destination allowlist, private-address
/// filter, or DNS policy: any absolute HTTP or HTTPS address is accepted.
/// </summary>
public static class AiChatCompletionsUrl
{
    private const string OperationPath = "/chat/completions";

    public static Uri Build(string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new ArgumentException("The AI Base URL is required.", nameof(baseUrl));
        }

        if (!Uri.TryCreate(baseUrl.Trim(), UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https"))
        {
            throw new ArgumentException(
                "The AI Base URL must be an absolute HTTP or HTTPS URL.", nameof(baseUrl));
        }

        var path = uri.AbsolutePath.TrimEnd('/');
        if (path.EndsWith(OperationPath, StringComparison.OrdinalIgnoreCase))
        {
            path = path[..^OperationPath.Length].TrimEnd('/');
        }

        var candidate = $"{uri.Scheme}://{uri.Authority}{path}{OperationPath}";
        return new Uri(candidate);
    }
}
