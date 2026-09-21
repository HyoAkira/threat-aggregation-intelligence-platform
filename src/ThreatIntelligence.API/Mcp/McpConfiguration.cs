namespace ThreatIntelligence.API.Mcp;

/// <summary>Configuration for the public MCP endpoint.</summary>
public sealed class McpConfiguration
{
    /// <summary>Whether the MCP endpoint is enabled. Defaults to true.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Comma-separated list of trusted proxy IPs for client address extraction.</summary>
    public IReadOnlyList<string> TrustedProxyIps { get; set; } = [];

    /// <summary>Allowed request origins for browser-based MCP clients (CORS).</summary>
    public IReadOnlyList<string> AllowedOrigins { get; set; } = [];

    /// <summary>Optional accepted Host header values. Empty means use deployment host validation.</summary>
    public IReadOnlyList<string> AcceptedHosts { get; set; } = [];

    /// <summary>Maximum concurrent MCP requests per client IP.</summary>
    public int MaxConcurrentRequestsPerClient { get; set; } = 5;

    /// <summary>Maximum concurrent MCP requests host-wide.</summary>
    public int MaxConcurrentRequestsHost { get; set; } = 50;

    /// <summary>Sliding window request limit per client IP.</summary>
    public int MaxRequestsPerClientPerWindow { get; set; } = 100;

    /// <summary>Sliding window duration for per-client rate limiting.</summary>
    public TimeSpan RequestWindow { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>Host-wide request cap per window.</summary>
    public int MaxRequestsHostPerWindow { get; set; } = 1000;
}
