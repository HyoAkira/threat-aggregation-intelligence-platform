using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThreatIntelligence.API.Controllers;
using ThreatIntelligence.Application.Admin;
using ThreatIntelligence.Domain.Entities;
using ThreatIntelligence.Domain.Enums;
using ThreatIntelligence.Infrastructure.Admin;

namespace ThreatIntelligence.IntegrationTests;

[Collection("sqlserver")]
public sealed class AdminProviderOperationsControllerTests(SqlServerFixture fixture) : IAsyncLifetime
{
    public async Task InitializeAsync() => await fixture.ResetDataAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Dashboard_returns_modes_and_null_times_for_never_run_providers()
    {
        var controller = CreateController();

        var result = await controller.Providers(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var providers = Assert.IsAssignableFrom<IReadOnlyList<AdminProviderDashboardItem>>(ok.Value);
        var threatFox = providers.Single(provider => provider.Name == "ThreatFox");
        var phishTank = providers.Single(provider => provider.Name == "PhishTank");

        Assert.Null(threatFox.LastFetchStatus);
        Assert.Null(threatFox.LastFetchAt);
        Assert.Null(threatFox.NextFetchTime);
        Assert.Equal(["recent", "full"], threatFox.AvailableModes);
        Assert.Equal(["full"], phishTank.AvailableModes);
    }

    [Fact]
    public async Task Dashboard_returns_status_times_and_no_secrets_after_a_completed_fetch()
    {
        await using (var context = fixture.CreateContext())
        {
            var provider = await context.Providers.SingleAsync(item => item.Id == 1);
            provider.MarkFetchSucceeded(DateTime.UtcNow.AddMinutes(-5));
            await context.SaveChangesAsync();
        }

        var controller = CreateController();
        var result = await controller.Providers(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var providers = Assert.IsAssignableFrom<IReadOnlyList<AdminProviderDashboardItem>>(ok.Value);
        var threatFox = providers.Single(provider => provider.Name == "ThreatFox");
        Assert.Equal("success", threatFox.LastFetchStatus);
        Assert.NotNull(threatFox.LastFetchAt);
        Assert.NotNull(threatFox.NextFetchTime);
        Assert.DoesNotContain(
            typeof(AdminProviderDashboardItem).GetProperties(),
            property => property.Name.Contains("key", StringComparison.OrdinalIgnoreCase)
                || property.Name.Contains("secret", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task History_is_newest_first_and_paged()
    {
        var oldest = Guid.NewGuid();
        var newest = Guid.NewGuid();
        await using (var context = fixture.CreateContext())
        {
            context.FetchHistory.Add(new FetchHistory(
                oldest, 1, FetchTrigger.Scheduled, FetchMode.Recent, FetchStatus.Queued, DateTime.UtcNow.AddMinutes(-10)));
            context.FetchHistory.Add(new FetchHistory(
                newest, 1, FetchTrigger.Manual, FetchMode.Full, FetchStatus.Queued, DateTime.UtcNow));
            await context.SaveChangesAsync();
        }

        var controller = CreateController();
        var result = await controller.History(1, null, null, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var page = Assert.IsType<AdminProviderHistoryPage>(ok.Value);
        Assert.Equal(2, page.TotalCount);
        Assert.Equal(newest, page.Items[0].JobId);
        Assert.Equal(oldest, page.Items[1].JobId);
        Assert.Equal("manual", page.Items[0].Trigger);
        Assert.Equal("full", page.Items[0].Mode);
    }

    [Fact]
    public async Task History_rejects_invalid_paging_and_unknown_provider()
    {
        var controller = CreateController();

        Assert.IsType<BadRequestObjectResult>((await controller.History(1, 0, null, CancellationToken.None)).Result);
        Assert.IsType<BadRequestObjectResult>((await controller.History(1, null, 101, CancellationToken.None)).Result);
        Assert.IsType<NotFoundResult>((await controller.History(999, null, null, CancellationToken.None)).Result);
    }

    [Fact]
    public async Task Logical_job_is_addressable_and_unknown_job_is_404()
    {
        var jobId = Guid.NewGuid();
        await using (var context = fixture.CreateContext())
        {
            context.FetchHistory.Add(new FetchHistory(
                jobId, 1, FetchTrigger.Manual, FetchMode.Recent, FetchStatus.Running, DateTime.UtcNow));
            await context.SaveChangesAsync();
        }

        var controller = CreateController();
        var ok = Assert.IsType<OkObjectResult>((await controller.Job(jobId, CancellationToken.None)).Result);
        var item = Assert.IsType<AdminFetchHistoryItem>(ok.Value);
        Assert.Equal("running", item.Status);

        Assert.IsType<NotFoundResult>((await controller.Job(Guid.NewGuid(), CancellationToken.None)).Result);
    }

    private AdminProviderOperationsController CreateController() =>
        new(new AdminProviderOperationsReader(fixture.CreateContextFactory()));
}
