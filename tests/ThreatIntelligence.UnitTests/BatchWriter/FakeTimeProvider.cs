namespace ThreatIntelligence.UnitTests.BatchWriter;

internal sealed class FakeTimeProvider(DateTime nowUtc) : TimeProvider
{
    private readonly DateTimeOffset _now = new DateTimeOffset(nowUtc, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => _now;
}