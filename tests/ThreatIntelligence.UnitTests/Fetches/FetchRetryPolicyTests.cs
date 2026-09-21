using ThreatIntelligence.Application.Fetches;

namespace ThreatIntelligence.UnitTests.Fetches;

public sealed class FetchRetryPolicyTests
{
    [Fact]
    public void Canonical_intervals_match_the_decided_ten_thirty_sixty_policy()
    {
        Assert.Equal(
            [TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(1)],
            FetchRetryPolicy.Intervals);
    }
}
