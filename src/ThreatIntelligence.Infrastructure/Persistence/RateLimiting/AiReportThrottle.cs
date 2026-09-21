using System.Threading.RateLimiting;

namespace ThreatIntelligence.Infrastructure.Persistence.RateLimiting;

/// <summary>
/// Holds every lease acquired for one AI report run. Disposing it releases the concurrency slots.
/// </summary>
public sealed class AiReportRunScope : IDisposable
{
    private readonly RateLimitLease? _requestLease;
    private readonly RateLimitLease _userLease;
    private readonly RateLimitLease _hostLease;

    internal AiReportRunScope(RateLimitLease? requestLease, RateLimitLease userLease, RateLimitLease hostLease)
    {
        _requestLease = requestLease;
        _userLease = userLease;
        _hostLease = hostLease;
    }

    public void Dispose()
    {
        _hostLease.Dispose();
        _userLease.Dispose();
        _requestLease?.Dispose();
    }
}

/// <summary>
/// Endpoint-specific AI report limits: a per-user sliding request budget, a per-user concurrent-run
/// cap, and a host-wide aggregate cap. Partitioning keys off the authenticated username, never a
/// caller-supplied IP or prompt content. Limits are per process; multi-replica deployments enforce
/// an aggregate upstream budget at the gateway or a shared store.
/// </summary>
public sealed class AiReportThrottle : IDisposable
{
    private const int PerUserRequestLimit = 10;
    private static readonly TimeSpan PerUserWindow = TimeSpan.FromMinutes(10);
    private const int PerUserConcurrencyLimit = 1;
    private const int HostConcurrencyLimit = 4;
    private const double DefaultRetryAfterSeconds = 5;

    private readonly PartitionedRateLimiter<string> _userRequestLimiter;
    private readonly ConcurrencyLimiter _userConcurrencyLimiter;
    private readonly ConcurrencyLimiter _hostConcurrencyLimiter;

    public AiReportThrottle()
    {
        _userRequestLimiter = PartitionedRateLimiter.Create<string, string>(username =>
            RateLimitPartition.GetSlidingWindowLimiter(
                username,
                _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = PerUserRequestLimit,
                    Window = PerUserWindow,
                    SegmentsPerWindow = 4,
                    QueueLimit = 0,
                }));

        _userConcurrencyLimiter = new ConcurrencyLimiter(new ConcurrencyLimiterOptions
        {
            PermitLimit = PerUserConcurrencyLimit,
            QueueLimit = 0,
        });

        _hostConcurrencyLimiter = new ConcurrencyLimiter(new ConcurrencyLimiterOptions
        {
            PermitLimit = HostConcurrencyLimit,
            QueueLimit = 0,
        });
    }

    /// <summary>
    /// Attempts to reserve one report slot for the authenticated user. Returns a scope the caller
    /// must dispose when the run ends, or null with a <c>Retry-After</c> suggestion when refused.
    /// </summary>
    public async Task<(AiReportRunScope? Scope, double RetryAfterSeconds)> TryAcquireAsync(
        string username,
        CancellationToken cancellationToken)
    {
        var requestLease = await _userRequestLimiter.AcquireAsync(username, 1, cancellationToken);
        if (!requestLease.IsAcquired)
        {
            var retry = requestLease.TryGetMetadata(MetadataName.RetryAfter, out var retryValue)
                && retryValue is TimeSpan span
                ? span.TotalSeconds
                : PerUserWindow.TotalSeconds;
            requestLease.Dispose();
            return (null, retry);
        }

        var userLease = await _userConcurrencyLimiter.AcquireAsync(1, cancellationToken);
        if (!userLease.IsAcquired)
        {
            userLease.Dispose();
            requestLease.Dispose();
            return (null, DefaultRetryAfterSeconds);
        }

        var hostLease = await _hostConcurrencyLimiter.AcquireAsync(1, cancellationToken);
        if (!hostLease.IsAcquired)
        {
            hostLease.Dispose();
            userLease.Dispose();
            requestLease.Dispose();
            return (null, DefaultRetryAfterSeconds);
        }

        return (new AiReportRunScope(requestLease, userLease, hostLease), 0);
    }

    public void Dispose()
    {
        _userRequestLimiter.Dispose();
        _userConcurrencyLimiter.Dispose();
        _hostConcurrencyLimiter.Dispose();
    }
}
