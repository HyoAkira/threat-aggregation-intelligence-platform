using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using ThreatIntelligence.Application.Abstractions.Secrets;
using ThreatIntelligence.Application.AI;

namespace ThreatIntelligence.Infrastructure.AI;

/// <summary>
/// Bounded runtime limits for the OpenAI-compatible chat-completions call. Values are code-owned
/// so upstream latency and payload sizes stay predictable regardless of Admin configuration.
/// </summary>
public sealed record AiClientOptions(
    TimeSpan RequestTimeout,
    int MaxUpstreamChunkBytes,
    int MaxEventPayloadBytes)
{
    public static AiClientOptions Default { get; } = new(
        TimeSpan.FromMinutes(5),
        1_048_576,
        262_144);
}

/// <summary>
/// Typed OpenAI-compatible chat-completions client. The Base URL is normalized and the fixed
/// operation path is appended; the optional API key is resolved immediately before the operation
/// and only placed in the upstream Authorization header. Upstream SSE chunks are parsed into
/// assistant deltas; nothing upstream-specific reaches the browser.
/// </summary>
public sealed class OpenAiCompatibleChatClient(
    HttpClient httpClient,
    ISecretStore secretStore,
    AiClientOptions options,
    ILogger<OpenAiCompatibleChatClient> logger) : IChatCompletionClient
{
    private static readonly string[] TerminalReasons = ["stop", "length", "content_filter"];

    public async IAsyncEnumerable<ChatCompletionDelta> StreamAsync(
        ChatCompletionRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var url = AiChatCompletionsUrl.Build(request.BaseUrl);
        var chunks = await RequestStreamAsync(request, url, cancellationToken);
        await foreach (var delta in chunks.WithCancellation(cancellationToken))
        {
            yield return delta;
        }
    }

    private async Task<IAsyncEnumerable<ChatCompletionDelta>> RequestStreamAsync(
        ChatCompletionRequest request,
        Uri url,
        CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(options.RequestTimeout);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
        httpRequest.Content = new StringContent(
            JsonSerializer.Serialize(new ChatCompletionWireRequest(
                request.ModelId,
                request.Messages.Select(message => new ChatMessageWire(message.Role, message.Content)).ToList(),
                true,
                request.Temperature,
                request.MaxTokens),
                JsonOptions),
            Encoding.UTF8,
            "application/json");
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        var apiKey = await secretStore.ResolveAsync(
            ManagedSecretNames.OpenAiCompatibleServerApiKey, timeoutCts.Token);
        if (!string.IsNullOrEmpty(apiKey))
        {
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(
                httpRequest, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AiUpstreamException(AiErrorCodes.UpstreamTimeout, "The model server did not respond in time.");
        }

        if (!response.IsSuccessStatusCode)
        {
            var status = (int)response.StatusCode;
            logger.LogInformation("AI upstream request failed with status {Status}", status);
            response.Dispose();
            throw new AiUpstreamException(
                status == 408 ? AiErrorCodes.UpstreamTimeout : AiErrorCodes.UpstreamFailure,
                "The model server rejected the request.",
                status);
        }

        var stream = await response.Content.ReadAsStreamAsync(timeoutCts.Token);
        return ParseStreamAsync(
            new StreamReader(stream, Encoding.UTF8, leaveOpen: false), response, cancellationToken);
    }

    private async IAsyncEnumerable<ChatCompletionDelta> ParseStreamAsync(
        StreamReader reader,
        HttpResponseMessage response,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        try
        {
            var buffer = new StringBuilder();
            while (await reader.ReadLineAsync(cancellationToken) is { } line)
            {
                if (line.Length == 0)
                {
                    if (buffer.Length == 0) continue;
                    var frame = buffer.ToString();
                    buffer.Clear();

                    var delta = ExtractDelta(frame);
                    if (delta is { Length: > 0 })
                    {
                        yield return new ChatCompletionDelta(delta);
                    }

                    if (frame.Contains("[DONE]", StringComparison.Ordinal))
                    {
                        yield break;
                    }

                    continue;
                }

                if (line.StartsWith("data:", StringComparison.Ordinal))
                {
                    if (buffer.Length > 0) buffer.Append('\n');
                    buffer.Append(line[5..].TrimStart());
                }
                else if (buffer.Length > 0)
                {
                    // Continuation of a multi-line SSE data frame.
                    buffer.Append(line);
                }
                else if (line.StartsWith(':') || line.StartsWith("event:", StringComparison.Ordinal) ||
                         line.StartsWith("id:", StringComparison.Ordinal) || line.StartsWith("retry:", StringComparison.Ordinal))
                {
                    // SSE comment or metadata line; chat-completions frames are data-only.
                }
                else
                {
                    throw new AiUpstreamException(
                        AiErrorCodes.UnsupportedUpstreamResponse,
                        "The model server returned an unsupported streaming response.");
                }
            }

            if (buffer.Length > 0)
            {
                throw new AiUpstreamException(
                    AiErrorCodes.UnsupportedUpstreamResponse,
                    "The model server closed the stream mid-frame.");
            }
        }
        finally
        {
            reader.Dispose();
            response.Dispose();
        }
    }

    private string? ExtractDelta(string data)
    {
        if (data.Contains("[DONE]", StringComparison.Ordinal)) return null;

        ChatChunk? chunk;
        try
        {
            chunk = JsonSerializer.Deserialize<ChatChunk>(data, JsonOptions);
        }
        catch (JsonException)
        {
            throw new AiUpstreamException(
                AiErrorCodes.UnsupportedUpstreamResponse,
                "The model server returned a malformed streaming chunk.");
        }

        var choice = chunk?.Choices?.FirstOrDefault();
        if (choice?.Delta?.Content is { Length: > 0 } content)
        {
            return content;
        }

        return null;
    }

    internal sealed record ChatChunk(
        [property: JsonPropertyName("choices")] ChatChoiceWire[]? Choices);

    internal sealed record ChatChoiceWire(
        [property: JsonPropertyName("delta")] ChatDeltaWire? Delta,
        [property: JsonPropertyName("finish_reason")] string? FinishReason);

    internal sealed record ChatDeltaWire(
        [property: JsonPropertyName("content")] string? Content);

    internal sealed record ChatCompletionWireRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] IReadOnlyList<ChatMessageWire> Messages,
        [property: JsonPropertyName("stream")] bool Stream,
        [property: JsonPropertyName("temperature")] double Temperature,
        [property: JsonPropertyName("max_tokens")] int MaxTokens);

    internal sealed record ChatMessageWire(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web);
}
