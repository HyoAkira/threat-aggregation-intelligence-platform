using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ThreatIntelligence.API.Controllers;
using ThreatIntelligence.API.Errors;
using ThreatIntelligence.Application.Abstractions.Persistence;
using ThreatIntelligence.Application.Admin;
using ThreatIntelligence.Domain.Entities;
using ThreatIntelligence.Domain.Enums;
using ThreatIntelligence.Infrastructure.Admin;
using ThreatIntelligence.Infrastructure.Persistence;

namespace ThreatIntelligence.IntegrationTests;

/// <summary>
/// Timeline aggregation, timezone/DST assignment, and range validation. Persisted instants stay
/// UTC while the local calendar bucket is derived from the requested IANA timezone.
/// </summary>
[Collection("sqlserver")]
public sealed class AdminSummaryTimelineControllerTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private static readonly DateTime UtcRangeStart = new(2026, 9, 14, 0, 0, 0, DateTimeKind.Utc);

    public async Task InitializeAsync() => await fixture.ResetDataAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Timeline_aggregates_successes_by_local_day_and_zero_fills_gaps()
    {
        await using var context = fixture.CreateContext();
        await InsertSuccessAsync(context, new DateTime(2026, 9, 14, 10, 0, 0, DateTimeKind.Utc), added: 5, updated: 2);
        await InsertSuccessAsync(context, new DateTime(2026, 9, 14, 20, 0, 0, DateTimeKind.Utc), added: 3, updated: 1);
        await InsertSuccessAsync(context, new DateTime(2026, 9, 16, 1, 0, 0, DateTimeKind.Utc), added: 7, updated: 0);
        await InsertFailedAsync(context, new DateTime(2026, 9, 15, 10, 0, 0, DateTimeKind.Utc));
        await InsertRunningAsync(context, new DateTime(2026, 9, 15, 11, 0, 0, DateTimeKind.Utc));

        var reader = new AdminSummaryTimelineReader(context);
        var timeline = await reader.GetTimelineAsync(
            UtcRangeStart,
            UtcRangeStart.AddDays(7),
            TimeZoneInfo.Utc,
            CancellationToken.None);

        Assert.True(timeline.HasSuccessfulFetches);
        Assert.Equal("UTC", timeline.TimeZone);
        Assert.Equal(UtcRangeStart.AddDays(7), timeline.To);
        Assert.Equal(7, timeline.Points.Count);

        Assert.Equal(new DateOnly(2026, 9, 14), timeline.Points[0].LocalDate);
        Assert.Equal(8, timeline.Points[0].RecordsAdded);
        Assert.Equal(3, timeline.Points[0].RecordsUpdated);
        Assert.Equal(new DateTime(2026, 9, 14, 0, 0, 0, DateTimeKind.Utc), timeline.Points[0].BucketStartUtc);

        Assert.Equal(new DateOnly(2026, 9, 15), timeline.Points[1].LocalDate);
        Assert.Equal(0, timeline.Points[1].RecordsAdded);
        Assert.Equal(0, timeline.Points[1].RecordsUpdated);

        Assert.Equal(new DateOnly(2026, 9, 16), timeline.Points[2].LocalDate);
        Assert.Equal(7, timeline.Points[2].RecordsAdded);
        Assert.Equal(0, timeline.Points[2].RecordsUpdated);

        Assert.Equal(new DateOnly(2026, 9, 20), timeline.Points[6].LocalDate);
        Assert.Equal(0, timeline.Points[6].RecordsAdded);
    }

    [Fact]
    public async Task Timeline_without_a_successful_fetch_is_zero_and_flagged()
    {
        await using var context = fixture.CreateContext();
        await InsertFailedAsync(context, new DateTime(2026, 9, 15, 10, 0, 0, DateTimeKind.Utc));
        await InsertRunningAsync(context, new DateTime(2026, 9, 15, 11, 0, 0, DateTimeKind.Utc));

        var reader = new AdminSummaryTimelineReader(context);
        var timeline = await reader.GetTimelineAsync(
            UtcRangeStart,
            UtcRangeStart.AddDays(7),
            TimeZoneInfo.Utc,
            CancellationToken.None);

        Assert.False(timeline.HasSuccessfulFetches);
        Assert.Equal(7, timeline.Points.Count);
        Assert.All(timeline.Points, point =>
        {
            Assert.Equal(0, point.RecordsAdded);
            Assert.Equal(0, point.RecordsUpdated);
        });
    }

    [Fact]
    public async Task Timeline_assigns_utc_completions_to_the_istanbul_local_day()
    {
        await using var context = fixture.CreateContext();
        // Local Sep 14 00:30 (+03:00) and local Sep 15 01:00 (+03:00).
        await InsertSuccessAsync(context, new DateTime(2026, 9, 13, 21, 30, 0, DateTimeKind.Utc), added: 2, updated: 0);
        await InsertSuccessAsync(context, new DateTime(2026, 9, 14, 22, 0, 0, DateTimeKind.Utc), added: 4, updated: 1);

        var zone = ResolveZone("Europe/Istanbul");
        var from = new DateTime(2026, 9, 13, 21, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 9, 16, 21, 0, 0, DateTimeKind.Utc);

        var reader = new AdminSummaryTimelineReader(context);
        var timeline = await reader.GetTimelineAsync(from, to, zone, CancellationToken.None);

        Assert.Equal(3, timeline.Points.Count);
        Assert.Equal(new DateOnly(2026, 9, 14), timeline.Points[0].LocalDate);
        Assert.Equal(2, timeline.Points[0].RecordsAdded);
        Assert.Equal(new DateTime(2026, 9, 13, 21, 0, 0, DateTimeKind.Utc), timeline.Points[0].BucketStartUtc);

        Assert.Equal(new DateOnly(2026, 9, 15), timeline.Points[1].LocalDate);
        Assert.Equal(4, timeline.Points[1].RecordsAdded);
        Assert.Equal(new DateTime(2026, 9, 14, 21, 0, 0, DateTimeKind.Utc), timeline.Points[1].BucketStartUtc);

        Assert.Equal(0, timeline.Points[2].RecordsAdded);
    }

    [Fact]
    public async Task Timeline_bucket_starts_follow_the_new_york_dst_transition()
    {
        await using var context = fixture.CreateContext();
        // 2026-03-09T03:30Z is local Mar 8 22:30 EST; 2026-03-09T04:30Z is local Mar 9 00:30 EDT.
        await InsertSuccessAsync(context, new DateTime(2026, 3, 9, 3, 30, 0, DateTimeKind.Utc), added: 3, updated: 0);
        await InsertSuccessAsync(context, new DateTime(2026, 3, 9, 4, 30, 0, DateTimeKind.Utc), added: 5, updated: 0);

        var zone = ResolveZone("America/New_York");
        var from = new DateTime(2026, 3, 8, 5, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 3, 10, 4, 0, 0, DateTimeKind.Utc);

        var reader = new AdminSummaryTimelineReader(context);
        var timeline = await reader.GetTimelineAsync(from, to, zone, CancellationToken.None);

        Assert.Equal(2, timeline.Points.Count);
        Assert.Equal(new DateOnly(2026, 3, 8), timeline.Points[0].LocalDate);
        Assert.Equal(3, timeline.Points[0].RecordsAdded);
        Assert.Equal(new DateTime(2026, 3, 8, 5, 0, 0, DateTimeKind.Utc), timeline.Points[0].BucketStartUtc);

        Assert.Equal(new DateOnly(2026, 3, 9), timeline.Points[1].LocalDate);
        Assert.Equal(5, timeline.Points[1].RecordsAdded);
        Assert.Equal(new DateTime(2026, 3, 9, 4, 0, 0, DateTimeKind.Utc), timeline.Points[1].BucketStartUtc);
    }

    [Fact]
    public async Task Timeline_supports_a_fractional_kathmandu_offset()
    {
        await using var context = fixture.CreateContext();
        // Local Sep 14 23:45 and Sep 15 00:15 at UTC+05:45.
        await InsertSuccessAsync(context, new DateTime(2026, 9, 14, 18, 0, 0, DateTimeKind.Utc), added: 1, updated: 0);
        await InsertSuccessAsync(context, new DateTime(2026, 9, 14, 18, 30, 0, DateTimeKind.Utc), added: 2, updated: 0);

        var zone = ResolveZone("Asia/Kathmandu");
        var from = new DateTime(2026, 9, 13, 18, 15, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 9, 15, 18, 15, 0, DateTimeKind.Utc);

        var reader = new AdminSummaryTimelineReader(context);
        var timeline = await reader.GetTimelineAsync(from, to, zone, CancellationToken.None);

        Assert.Equal(2, timeline.Points.Count);
        Assert.Equal(new DateOnly(2026, 9, 14), timeline.Points[0].LocalDate);
        Assert.Equal(1, timeline.Points[0].RecordsAdded);
        Assert.Equal(new DateTime(2026, 9, 13, 18, 15, 0, DateTimeKind.Utc), timeline.Points[0].BucketStartUtc);

        Assert.Equal(new DateOnly(2026, 9, 15), timeline.Points[1].LocalDate);
        Assert.Equal(2, timeline.Points[1].RecordsAdded);
        Assert.Equal(new DateTime(2026, 9, 14, 18, 15, 0, DateTimeKind.Utc), timeline.Points[1].BucketStartUtc);
    }

    [Theory]
    [InlineData(null, "2026-09-21T00:00:00Z", "Europe/Istanbul", TimelineRequestValidator.InvalidRangeCode)]
    [InlineData("not-a-date", "2026-09-21T00:00:00Z", "Europe/Istanbul", TimelineRequestValidator.InvalidRangeCode)]
    [InlineData("2026-09-14T00:00:00", "2026-09-21T00:00:00Z", "Europe/Istanbul", TimelineRequestValidator.InvalidRangeCode)]
    [InlineData("2026-09-21T00:00:00Z", "2026-09-14T00:00:00Z", "Europe/Istanbul", TimelineRequestValidator.InvalidRangeCode)]
    [InlineData("2026-09-14T00:00:00Z", "2026-09-21T00:00:00Z", "Mars/Olympus", TimelineRequestValidator.InvalidTimeZoneCode)]
    [InlineData("2026-09-01T00:00:00Z", "2026-10-04T00:00:00Z", "UTC", TimelineRequestValidator.RangeTooLargeCode)]
    public async Task Invalid_timeline_requests_return_400_problem_without_querying_history(
        string? from,
        string? to,
        string? timeZone,
        string expectedCode)
    {
        await using var context = fixture.CreateContext();
        var controller = CreateController(context);

        var result = await controller.Timeline(
            from,
            to,
            timeZone,
            new ThrowingTimelineReader(),
            new TimeZoneResolver(),
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var problem = Assert.IsType<ApiProblemDetails>(badRequest.Value);
        Assert.Equal(expectedCode, problem.Code);
        Assert.Equal(400, problem.Status);
    }

    [Fact]
    public async Task Valid_timeline_request_returns_normalized_range_and_points()
    {
        await using var context = fixture.CreateContext();
        await InsertSuccessAsync(context, new DateTime(2026, 9, 14, 12, 0, 0, DateTimeKind.Utc), added: 6, updated: 2);

        var controller = CreateController(context);
        var result = await controller.Timeline(
            "2026-09-14T00:00:00Z",
            "2026-09-21T00:00:00Z",
            "UTC",
            new AdminSummaryTimelineReader(context),
            new TimeZoneResolver(),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var timeline = Assert.IsType<AdminTimeline>(ok.Value);
        Assert.True(timeline.HasSuccessfulFetches);
        Assert.Equal(new DateTime(2026, 9, 14, 0, 0, 0, DateTimeKind.Utc), timeline.From);
        Assert.Equal(7, timeline.Points.Count);
        Assert.Equal(6, timeline.Points[0].RecordsAdded);
    }

    [Fact]
    public void Explicit_utc_designator_is_required()
    {
        var resolver = new TimeZoneResolver();
        Assert.False(TimelineRequestValidator.Validate(
            "2026-09-14T03:00:00+03:00", "2026-09-21T00:00:00Z", "UTC", resolver).IsValid);
        Assert.True(TimelineRequestValidator.Validate(
            "2026-09-14T00:00:00+00:00", "2026-09-21T00:00:00Z", "UTC", resolver).IsValid);
    }

    private static AdminSummaryController CreateController(ThreatIntelligenceDbContext context)
    {
        return new AdminSummaryController(new AdminSummaryReader(context))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };
    }

    private static TimeZoneInfo ResolveZone(string id)
    {
        Assert.True(new TimeZoneResolver().TryResolve(id, out var zone), $"Timezone {id} should resolve.");
        return zone;
    }

    private static async Task InsertSuccessAsync(
        ThreatIntelligenceDbContext context,
        DateTime finishedAtUtc,
        int added,
        int updated)
    {
        context.FetchHistory.Add(new FetchHistory(
            Guid.NewGuid(),
            1,
            FetchTrigger.Scheduled,
            FetchMode.Recent,
            FetchStatus.Success,
            finishedAtUtc.AddMinutes(-1),
            finishedAtUtc,
            added,
            updated));
        await context.SaveChangesAsync();
    }

    private static async Task InsertFailedAsync(ThreatIntelligenceDbContext context, DateTime finishedAtUtc)
    {
        context.FetchHistory.Add(new FetchHistory(
            Guid.NewGuid(),
            1,
            FetchTrigger.Scheduled,
            FetchMode.Recent,
            FetchStatus.Failed,
            finishedAtUtc.AddMinutes(-1),
            finishedAtUtc,
            errorDetail: "provider unavailable"));
        await context.SaveChangesAsync();
    }

    private static async Task InsertRunningAsync(ThreatIntelligenceDbContext context, DateTime startedAtUtc)
    {
        context.FetchHistory.Add(new FetchHistory(
            Guid.NewGuid(),
            1,
            FetchTrigger.Scheduled,
            FetchMode.Recent,
            FetchStatus.Running,
            startedAtUtc));
        await context.SaveChangesAsync();
    }

    private sealed class ThrowingTimelineReader : IAdminSummaryTimelineReader
    {
        public Task<AdminTimeline> GetTimelineAsync(
            DateTime fromUtc,
            DateTime toUtc,
            TimeZoneInfo timeZone,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("History must not be queried for an invalid timeline request.");
    }
}
