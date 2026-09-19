using System.Text.Json.Nodes;

namespace ThreatIntelligence.Application.AI;

public sealed record IocChatMessage(
    string Role,
    string Content,
    IReadOnlyList<IocChatToolCall>? ToolCalls = null,
    string? ToolCallId = null);

public sealed record IocChatToolDefinition(string Name, string Description, JsonObject Parameters);

public sealed record IocChatToolCall(string Id, string Name, string Arguments);

public sealed record IocChatCompletionRequest(
    string BaseUrl,
    string ModelId,
    IReadOnlyList<IocChatMessage> Messages,
    IReadOnlyList<IocChatToolDefinition> Tools,
    int MaxTokens);

public sealed record IocChatCompletionResponse(
    string? Text,
    IReadOnlyList<IocChatToolCall> ToolCalls,
    bool ToolCallsSupported);

public interface IIocChatCompletionClient
{
    Task<IocChatCompletionResponse> CompleteAsync(
        IocChatCompletionRequest request,
        CancellationToken cancellationToken);
}
