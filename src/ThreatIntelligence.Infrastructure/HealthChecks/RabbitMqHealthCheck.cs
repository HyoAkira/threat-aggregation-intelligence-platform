using System.Net.Sockets;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using ThreatIntelligence.Infrastructure.Configuration;

namespace ThreatIntelligence.Infrastructure.HealthChecks;

public sealed class RabbitMqHealthCheck(RabbitMqOptions rabbitMqOptions) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(rabbitMqOptions.Host, rabbitMqOptions.Port, cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("RabbitMQ is unavailable.", exception);
        }
    }
}
