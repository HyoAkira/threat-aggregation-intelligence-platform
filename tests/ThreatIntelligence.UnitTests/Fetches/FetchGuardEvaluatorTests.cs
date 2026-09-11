using ThreatIntelligence.Domain.Entities;
using ThreatIntelligence.Domain.Enums;
using ThreatIntelligence.Infrastructure.Fetches;

namespace ThreatIntelligence.UnitTests.Fetches;

public sealed class FetchGuardEvaluatorTests
{
    [Theory]
    [InlineData("ThreatFox", FetchMode.Recent, true)]
    [InlineData("ThreatFox", FetchMode.Full, true)]
    [InlineData("PhishTank", FetchMode.Recent, false)]
    [InlineData("PhishTank", FetchMode.Full, true)]
    [InlineData("Unknown", FetchMode.Recent, false)]
    [InlineData("Unknown", FetchMode.Full, false)]
    public void Supports_matches_each_provider_mode_matrix(string name, FetchMode mode, bool expected)
    {
        var provider = new Provider(name, $"https://{name.ToLowerInvariant()}.example", 60);

        Assert.Equal(expected, FetchGuardEvaluator.Supports(provider, mode));
    }
}
