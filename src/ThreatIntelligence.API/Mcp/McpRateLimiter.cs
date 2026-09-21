using System.Threading.RateLimiting;

namespace ThreatIntelligence.API.Mcp;

/// <summary>Holds leases for an MCP request; disposing releases concurrency slots.</summary>
public sealed class McpRequestScope : IDisposable
{
    private readonly RateLimitLease? _requestLease;
    private readonly RateLimitLease _clientConcurrencyLease;
    private readonly RateLimitLease _hostConcurrencyLease;
    private readonly RateLimitLease _hostRequestLease;

    internal McpRequestScope(
        RateLimitLease? requestLease,
        RateLimitLease clientConcurrencyLease,
        RateLimitLease hostConcurrencyLease,
        RateLimitLease hostRequestLease)
    {
        _requestLease = requestLease;
        _clientConcurrencyLease = clientConcurrencyLease;
        _hostConcurrencyLease = hostConcurrencyLease;
        _hostRequestLease = hostRequestLease;
    }

    public void Dispose()
    {
        _hostConcurrencyLease.Dispose();
        _clientConcurrencyLease.Dispose();
        _hostRequestLease.Dispose();
        _requestLease?.Dispose();
    }
}

/// <summary>
/// Rate limiter for the public MCP endpoint.
/// Enforces per-client and host-wide request and concurrency limits.
/// Uses trusted proxy handling for client IP extraction.
/// </summary>
public sealed class McpRateLimiter : IDisposable
{
    private readonly McpConfiguration _config;
    private readonly PartitionedRateLimiter<string> _clientRequestLimiter;
    private readonly PartitionedRateLimiter<string> _clientConcurrencyLimiter;
    private readonly ConcurrencyLimiter _hostConcurrencyLimiter;
    private readonly PartitionedRateLimiter<string> _hostRequestLimiter;

    public McpRateLimiter(McpConfiguration config)
    {
        _config = config;

        _clientRequestLimiter = PartitionedRateLimiter.Create<string, string>(clientId =>
            RateLimitPartition.GetSlidingWindowLimiter(
                clientId,
                _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = _config.MaxRequestsPerClientPerWindow,
                    Window = _config.RequestWindow,
                    SegmentsPerWindow = 4,
                    QueueLimit = 0
                }));

        _clientConcurrencyLimiter = PartitionedRateLimiter.Create<string, string>(clientId =>
            RateLimitPartition.GetConcurrencyLimiter(
                clientId,
                _ => new ConcurrencyLimiterOptions
                {
                    PermitLimit = _config.MaxConcurrentRequestsPerClient,
                    QueueLimit = 0
                }));

        _hostConcurrencyLimiter = new ConcurrencyLimiter(new ConcurrencyLimiterOptions
        {
            PermitLimit = _config.MaxConcurrentRequestsHost,
            QueueLimit = 0
        });

        _hostRequestLimiter = PartitionedRateLimiter.Create<string, string>(_ =>
            RateLimitPartition.GetSlidingWindowLimiter(
                "host",
                _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = _config.MaxRequestsHostPerWindow,
                    Window = _config.RequestWindow,
                    SegmentsPerWindow = 4,
                    QueueLimit = 0
                }));
    }

    /// <summary>
    /// Attempts to acquire a rate limit slot for an MCP request.
    /// Returns the scope if successful, or null with retry-after seconds if denied.
    /// </summary>
    public async Task<(McpRequestScope? Scope, double RetryAfterSeconds)> TryAcquireAsync(
        string clientId,
        CancellationToken cancellationToken)
    {
        var requestLease = await _clientRequestLimiter.AcquireAsync(clientId, 1, cancellationToken);
        if (!requestLease.IsAcquired)
        {
            var retry = requestLease.TryGetMetadata(MetadataName.RetryAfter, out var retryValue) && retryValue is TimeSpan span
                ? span.TotalSeconds
                : _config.RequestWindow.TotalSeconds;
            requestLease.Dispose();
            return (null, retry);
        }

        var clientConcurrencyLease = await _clientConcurrencyLimiter.AcquireAsync(clientId, 1, cancellationToken);
        if (!clientConcurrencyLease.IsAcquired)
        {
            clientConcurrencyLease.Dispose();
            requestLease.Dispose();
            return (null, 5.0);
        }

        var hostConcurrencyLease = await _hostConcurrencyLimiter.AcquireAsync(1, cancellationToken);
        if (!hostConcurrencyLease.IsAcquired)
        {
            hostConcurrencyLease.Dispose();
            clientConcurrencyLease.Dispose();
            requestLease.Dispose();
            return (null, 5.0);
        }

        // Also check host request limit
        var hostRequestLease = await _hostRequestLimiter.AcquireAsync("host", 1, cancellationToken);
        if (!hostRequestLease.IsAcquired)
        {
            hostRequestLease.Dispose();
            hostConcurrencyLease.Dispose();
            clientConcurrencyLease.Dispose();
            requestLease.Dispose();
            return (null, 5.0);
        }

        return (new McpRequestScope(requestLease, clientConcurrencyLease, hostConcurrencyLease, hostRequestLease), 0);
    }

    /// <summary>Extracts the client IP from the request, handling trusted proxies.</summary>
    public static string ExtractClientId(HttpRequest request, IReadOnlyList<string> trustedProxyIps)
    {
        // Check if the request came through a trusted proxy
        if (trustedProxyIps.Count > 0)
        {
            var remoteIp = request.HttpContext.Connection.RemoteIpAddress;
            if (remoteIp is not null && trustedProxyIps.Any(proxy =>
                proxy.Equals(remoteIp.ToString(), StringComparison.OrdinalIgnoreCase) ||
                proxy.Equals(remoteIp.MapToIPv6().ToString(), StringComparison.OrdinalIgnoreCase)))
            {
                // Use X-Forwarded-For header, but only the first untrusted IP
                if (request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor) &&
                    !string.IsNullOrWhiteSpace(forwardedFor))
                {
                    // Parse the chain and find the first client IP
                    var addresses = forwardedFor.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    if (addresses.Length > 0)
                    {
                        // The leftmost IP is the original client
                        return addresses[0];
                    }
                }
            }
        }

        // Fall back to remote IP
        return request.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    public void Dispose()
    {
        _clientRequestLimiter.Dispose();
        _clientConcurrencyLimiter.Dispose();
        _hostConcurrencyLimiter.Dispose();
        _hostRequestLimiter.Dispose();
    }
}
