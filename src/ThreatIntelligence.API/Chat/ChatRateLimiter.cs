using System.Threading.RateLimiting;

namespace ThreatIntelligence.API.Chat;

public sealed class ChatRateLimiter : IDisposable
{
    private readonly PartitionedRateLimiter<string> requests = PartitionedRateLimiter.Create<string, string>(key =>
        RateLimitPartition.GetSlidingWindowLimiter(key, _ => new SlidingWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            SegmentsPerWindow = 4,
            QueueLimit = 0
        }));
    private readonly PartitionedRateLimiter<string> concurrency = PartitionedRateLimiter.Create<string, string>(key =>
        RateLimitPartition.GetConcurrencyLimiter(key, _ => new ConcurrencyLimiterOptions
        {
            PermitLimit = 1,
            QueueLimit = 0
        }));
    private readonly ConcurrencyLimiter host = new(new ConcurrencyLimiterOptions { PermitLimit = 10, QueueLimit = 0 });

    public async Task<ChatRateLimitLease?> AcquireAsync(string clientKey, CancellationToken cancellationToken)
    {
        var request = await requests.AcquireAsync(clientKey, 1, cancellationToken);
        if (!request.IsAcquired) { request.Dispose(); return null; }
        var user = await concurrency.AcquireAsync(clientKey, 1, cancellationToken);
        if (!user.IsAcquired) { request.Dispose(); user.Dispose(); return null; }
        var aggregate = await host.AcquireAsync(1, cancellationToken);
        if (!aggregate.IsAcquired) { request.Dispose(); user.Dispose(); aggregate.Dispose(); return null; }
        return new ChatRateLimitLease(request, user, aggregate);
    }

    public void Dispose()
    {
        requests.Dispose();
        concurrency.Dispose();
        host.Dispose();
    }
}

public sealed class ChatRateLimitLease(RateLimitLease request, RateLimitLease user, RateLimitLease aggregate) : IDisposable
{
    public void Dispose()
    {
        aggregate.Dispose();
        user.Dispose();
        request.Dispose();
    }
}