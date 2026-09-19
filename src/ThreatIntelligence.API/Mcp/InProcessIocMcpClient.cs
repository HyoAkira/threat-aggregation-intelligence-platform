using System.IO.Pipelines;
using System.Reflection;
using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Microsoft.Extensions.Logging;
using ThreatIntelligence.Application.AI;

namespace ThreatIntelligence.API.Mcp;

/// <summary>
/// Hosts the registered public IOC tools behind an in-memory MCP server/client pair.
/// Chat therefore exercises the same MCP tool contract as remote clients without self-HTTP.
/// </summary>
public sealed class InProcessIocMcpClient(
    IServiceProvider services,
    ILoggerFactory loggerFactory) : IInProcessIocMcpClient
{
    public async Task<string> CallToolAsync(
        string toolName,
        IReadOnlyDictionary<string, object?> arguments,
        CancellationToken cancellationToken)
    {
        var target = services.GetRequiredService<PublicIocMcpServer>();
        var methods = typeof(PublicIocMcpServer).GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => method.GetCustomAttribute<McpServerToolAttribute>()?.Name == toolName)
            .ToArray();
        if (methods.Length != 1)
            throw new InvalidOperationException($"The MCP tool '{toolName}' is not allowlisted.");

        var tools = new McpServerPrimitiveCollection<McpServerTool>();
        foreach (var method in methods)
        {
            tools.Add(McpServerTool.Create(
                method,
                target,
                new McpServerToolCreateOptions { Services = services }));
        }

        var clientToServer = new Pipe();
        var serverToClient = new Pipe();
        await using var server = McpServer.Create(
            new StreamServerTransport(
                clientToServer.Reader.AsStream(),
                serverToClient.Writer.AsStream(),
                "in-process-public-ioc-mcp",
                loggerFactory),
            new McpServerOptions { ToolCollection = tools },
            loggerFactory,
            services);

        var serverTask = server.RunAsync(cancellationToken);
        await using var client = await McpClient.CreateAsync(
            new StreamClientTransport(
                clientToServer.Writer.AsStream(),
                serverToClient.Reader.AsStream(),
                loggerFactory),
            cancellationToken: cancellationToken);

        var result = await client.CallToolAsync(toolName, arguments, cancellationToken: cancellationToken);
        if (result.IsError == true)
            throw new InvalidOperationException($"The MCP tool '{toolName}' returned an error.");

        var text = string.Join(
            "\n",
            result.Content.OfType<TextContentBlock>().Select(block => block.Text));
        _ = serverTask.ContinueWith(_ => { }, TaskScheduler.Default);
        return text;
    }
}