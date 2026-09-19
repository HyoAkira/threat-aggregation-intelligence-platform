namespace ThreatIntelligence.Application.AI;

/// <summary>Invokes the public IOC MCP tool contract from an in-process client.</summary>
public interface IInProcessIocMcpClient
{
    Task<string> CallToolAsync(
        string toolName,
        IReadOnlyDictionary<string, object?> arguments,
        CancellationToken cancellationToken);
}