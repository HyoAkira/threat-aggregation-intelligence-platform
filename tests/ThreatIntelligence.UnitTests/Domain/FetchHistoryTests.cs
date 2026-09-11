using ThreatIntelligence.Domain.Entities;
using ThreatIntelligence.Domain.Enums;

namespace ThreatIntelligence.UnitTests.Domain;

public class FetchHistoryTests
{
    private static readonly DateTime Utc = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Guid JobId = Guid.NewGuid();

    [Fact]
    public void Empty_jobId_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => new FetchHistory(
            Guid.Empty, 1, FetchTrigger.Scheduled, FetchMode.Recent, FetchStatus.Running, Utc));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Non_positive_providerId_is_rejected(int providerId)
    {
        Assert.Throws<ArgumentException>(() => new FetchHistory(
            JobId, providerId, FetchTrigger.Scheduled, FetchMode.Recent, FetchStatus.Running, Utc));
    }

    [Fact]
    public void Non_utc_startedAt_is_rejected()
    {
        var local = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Local);
        Assert.Throws<ArgumentException>(() => new FetchHistory(
            JobId, 1, FetchTrigger.Scheduled, FetchMode.Recent, FetchStatus.Running, local));
    }

    [Fact]
    public void Negative_added_counter_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => new FetchHistory(
            JobId, 1, FetchTrigger.Scheduled, FetchMode.Recent, FetchStatus.Running, Utc, recordsAdded: -1));
    }

    [Fact]
    public void Negative_updated_counter_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => new FetchHistory(
            JobId, 1, FetchTrigger.Scheduled, FetchMode.Recent, FetchStatus.Running, Utc, recordsUpdated: -1));
    }

    [Fact]
    public void FinishedAt_earlier_than_startedAt_is_rejected()
    {
        Assert.Throws<InvalidOperationException>(() => new FetchHistory(
            JobId, 1, FetchTrigger.Scheduled, FetchMode.Recent, FetchStatus.Success,
            Utc, finishedAt: Utc.AddMinutes(-1)));
    }

    [Fact]
    public void Running_with_finish_time_is_rejected()
    {
        Assert.Throws<InvalidOperationException>(() => new FetchHistory(
            JobId, 1, FetchTrigger.Scheduled, FetchMode.Recent, FetchStatus.Running,
            Utc, finishedAt: Utc.AddMinutes(1)));
    }

    [Fact]
    public void Running_with_terminal_error_is_rejected()
    {
        Assert.Throws<InvalidOperationException>(() => new FetchHistory(
            JobId, 1, FetchTrigger.Scheduled, FetchMode.Recent, FetchStatus.Running,
            Utc, errorDetail: "boom"));
    }

    [Fact]
    public void Success_without_finish_time_is_rejected()
    {
        Assert.Throws<InvalidOperationException>(() => new FetchHistory(
            JobId, 1, FetchTrigger.Scheduled, FetchMode.Recent, FetchStatus.Success, Utc));
    }

    [Fact]
    public void Success_with_terminal_error_is_rejected()
    {
        Assert.Throws<InvalidOperationException>(() => new FetchHistory(
            JobId, 1, FetchTrigger.Scheduled, FetchMode.Recent, FetchStatus.Success,
            Utc, finishedAt: Utc.AddMinutes(1), errorDetail: "boom"));
    }

    [Fact]
    public void Failed_without_finish_time_is_rejected()
    {
        Assert.Throws<InvalidOperationException>(() => new FetchHistory(
            JobId, 1, FetchTrigger.Scheduled, FetchMode.Recent, FetchStatus.Failed, Utc, errorDetail: "boom"));
    }

    [Fact]
    public void Failed_with_blank_error_is_rejected()
    {
        Assert.Throws<InvalidOperationException>(() => new FetchHistory(
            JobId, 1, FetchTrigger.Scheduled, FetchMode.Recent, FetchStatus.Failed,
            Utc, finishedAt: Utc.AddMinutes(1), errorDetail: "  "));
    }

    [Fact]
    public void Failed_with_positive_counter_is_rejected()
    {
        Assert.Throws<InvalidOperationException>(() => new FetchHistory(
            JobId, 1, FetchTrigger.Scheduled, FetchMode.Recent, FetchStatus.Failed,
            Utc, finishedAt: Utc.AddMinutes(1), recordsAdded: 1, errorDetail: "boom"));
    }

    [Fact]
    public void Failed_with_zero_counters_and_error_is_accepted()
    {
        var history = new FetchHistory(
            JobId, 1, FetchTrigger.Scheduled, FetchMode.Recent, FetchStatus.Failed,
            Utc, finishedAt: Utc.AddMinutes(1), recordsAdded: 0, recordsUpdated: 0, errorDetail: "boom");

        Assert.Equal(FetchStatus.Failed, history.Status);
        Assert.Equal("boom", history.ErrorDetail);
    }
}
