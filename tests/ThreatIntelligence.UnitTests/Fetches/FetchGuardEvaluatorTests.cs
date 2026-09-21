using ThreatIntelligence.Domain.Entities;
using ThreatIntelligence.Domain.Enums;
using ThreatIntelligence.Infrastructure.Fetches;

namespace ThreatIntelligence.UnitTests.Fetches;

public sealed class FetchGuardEvaluatorTests
{
    [Fact]
    public void Supports_reads_the_persisted_provider_configuration()
    {
        var recentOnly = new Provider(
            "Custom", "https://custom.example", 60,
            defaultMode: FetchMode.Recent, supportedModes: ProviderFetchModes.Recent);
        var fullOnly = new Provider(
            "PhishTank", "https://phishtank.example", 480,
            defaultMode: FetchMode.Full, supportedModes: ProviderFetchModes.Full);
        var both = new Provider("ThreatFox", "https://threatfox.example", 5);

        Assert.True(FetchGuardEvaluator.Supports(recentOnly, FetchMode.Recent));
        Assert.False(FetchGuardEvaluator.Supports(recentOnly, FetchMode.Full));

        Assert.False(FetchGuardEvaluator.Supports(fullOnly, FetchMode.Recent));
        Assert.True(FetchGuardEvaluator.Supports(fullOnly, FetchMode.Full));

        Assert.True(FetchGuardEvaluator.Supports(both, FetchMode.Recent));
        Assert.True(FetchGuardEvaluator.Supports(both, FetchMode.Full));
    }
}
