using ThreatIntelligence.Application.BatchWriter;

namespace ThreatIntelligence.UnitTests.BatchWriter;

public sealed class FalsePositiveEvaluatorTests
{
    private static readonly DateTime NowUtc = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private readonly FalsePositiveEvaluator _evaluator = new(new FakeTimeProvider(NowUtc));

    [Fact]
    public void Row_older_than_180_days_is_false_positive()
    {
        var olderThanBoundary = NowUtc.AddDays(-181);
        Assert.True(_evaluator.IsFalsePositive(olderThanBoundary));
    }

    [Fact]
    public void Row_less_than_180_days_is_not_false_positive()
    {
        var withinBoundary = NowUtc.AddDays(-179);
        Assert.False(_evaluator.IsFalsePositive(withinBoundary));
    }

    [Fact]
    public void Row_exactly_on_the_180_day_boundary_is_not_false_positive()
    {
        var exactlyAtBoundary = NowUtc.AddDays(-180);
        Assert.False(_evaluator.IsFalsePositive(exactlyAtBoundary));
    }

    [Fact]
    public void Future_timestamp_is_not_false_positive()
    {
        Assert.False(_evaluator.IsFalsePositive(NowUtc.AddDays(1)));
    }
}