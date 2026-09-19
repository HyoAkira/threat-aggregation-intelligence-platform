using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using ThreatIntelligence.Application.AI;
using ThreatIntelligence.Application.Abstractions.Secrets;

namespace ThreatIntelligence.Infrastructure.AI;

/// <summary>Dedicated, non-streaming OpenAI-compatible client for bounded IOC chat tool rounds.</summary>
public sealed class OpenAiCompatibleIocChatClient(
    HttpClient httpClient,
    ISecretStore secretStore,
    ILogger<OpenAiCompatibleIocChatClient> logger) : IIocChatCompletionClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<IocChatCompletionResponse> CompleteAsync(
        IocChatCompletionRequest request,
        CancellationToken cancellationToken)
    {
        var key = await secretStore.ResolveAsync(
            ManagedSecretNames.IocChatOpenAiCompatibleServerApiKey,
            cancellationToken);
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new AiConfigurationException(AiErrorCodes.CredentialUnavailable, "IOC chat credentials are unavailable.");
        }

        using var message = new HttpRequestMessage(HttpMethod.Post, AiChatCompletionsUrl.Build(request.BaseUrl));
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        message.Content = new StringContent(JsonSerializer.Serialize(new
        {
            model = request.ModelId,
            stream = false,
            temperature = 0.2,
            max_tokens = request.MaxTokens,
            tool_choice = "auto",
            messages = request.Messages.Select(item => new
            {
                role = item.Role,
                content = item.Content,
                tool_call_id = item.ToolCallId,
                tool_calls = item.ToolCalls?.Select(call => new
                {
                    id = call.Id,
                    type = "function",
                    function = new { name = call.Name, arguments = call.Arguments }
                })
            }),
            tools = request.Tools.Select(tool => new
            {
                type = "function",
                function = new { name = tool.Name, description = tool.Description, parameters = tool.Parameters }
            })
        }, JsonOptions), Encoding.UTF8, "application/json");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(message, timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AiUpstreamException(AiErrorCodes.UpstreamTimeout, "The IOC chat model request timed out.");
        }
        catch (HttpRequestException)
        {
            throw new AiUpstreamException(AiErrorCodes.UpstreamFailure, "The IOC chat model request failed.");
        }

        using (response)
        {
            string body;
            try
            {
                body = await response.Content.ReadAsStringAsync(timeout.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new AiUpstreamException(AiErrorCodes.UpstreamTimeout, "The IOC chat model request timed out.");
            }
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("IOC chat upstream returned status {StatusCode}", (int)response.StatusCode);
                throw new AiUpstreamException(AiErrorCodes.UpstreamFailure, "The IOC chat model is unavailable.", (int)response.StatusCode);
            }

            try
            {
                var root = JsonNode.Parse(body)?.AsObject();
                var choice = root?["choices"]?.AsArray().FirstOrDefault()?.AsObject();
                var assistant = choice?["message"]?.AsObject();
                if (assistant is null) throw new JsonException();
                var toolCallsNode = assistant["tool_calls"];
                var calls = toolCallsNode?.AsArray()
                    .Select(node => node?.AsObject())
                    .Where(node => node is not null)
                    .Select(node => new IocChatToolCall(
                        node!["id"]?.GetValue<string>() ?? string.Empty,
                        node["function"]?["name"]?.GetValue<string>() ?? string.Empty,
                        node["function"]?["arguments"]?.GetValue<string>() ?? "{}"))
                    .ToArray() ?? [];
                if (calls.Any(call => string.IsNullOrWhiteSpace(call.Id) || string.IsNullOrWhiteSpace(call.Name)))
                {
                    throw new JsonException();
                }
                var text = assistant["content"]?.GetValue<string>();
                if (calls.Length == 0 && string.IsNullOrWhiteSpace(text))
                {
                    throw new AiUpstreamException(AiErrorCodes.UnsupportedUpstreamResponse, "The configured model did not return text or a tool call.");
                }
                return new IocChatCompletionResponse(text, calls, toolCallsNode is not null);
            }
            catch (AiUpstreamException)
            {
                throw;
            }
            catch (Exception exception) when (exception is JsonException or InvalidOperationException)
            {
                throw new AiUpstreamException(AiErrorCodes.UnsupportedUpstreamResponse, "The IOC chat model returned an unsupported response.");
            }
        }
    }
}
