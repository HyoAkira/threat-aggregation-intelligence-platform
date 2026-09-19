using System.Net;
using Microsoft.AspNetCore.Http;
using ThreatIntelligence.API.Mcp;

namespace ThreatIntelligence.UnitTests.Mcp;

public sealed class McpRateLimiterTests
{
    [Fact]
    public void ExtractClientId_ignores_forged_forwarded_header_from_untrusted_peer()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("198.51.100.10");
        context.Request.Headers["X-Forwarded-For"] = "203.0.113.55";

        var clientId = McpRateLimiter.ExtractClientId(
            context.Request,
            ["192.0.2.1"]);

        Assert.Equal("198.51.100.10", clientId);
    }

    [Fact]
    public void ExtractClientId_uses_first_forwarded_address_only_for_trusted_proxy()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.1");
        context.Request.Headers["X-Forwarded-For"] = "203.0.113.55, 192.0.2.2";

        var clientId = McpRateLimiter.ExtractClientId(
            context.Request,
            ["192.0.2.1"]);

        Assert.Equal("203.0.113.55", clientId);
    }

    [Fact]
    public async Task Distinct_clients_do_not_share_the_per_client_concurrency_slot()
    {
        using var limiter = new McpRateLimiter(new McpConfiguration
        {
            MaxConcurrentRequestsPerClient = 1,
            MaxConcurrentRequestsHost = 2,
            MaxRequestsPerClientPerWindow = 10,
            MaxRequestsHostPerWindow = 20
        });

        var first = await limiter.TryAcquireAsync("198.51.100.1", CancellationToken.None);
        var second = await limiter.TryAcquireAsync("198.51.100.2", CancellationToken.None);

        try
        {
            Assert.NotNull(first.Scope);
            Assert.NotNull(second.Scope);
        }
        finally
        {
            first.Scope?.Dispose();
            second.Scope?.Dispose();
        }
    }
}