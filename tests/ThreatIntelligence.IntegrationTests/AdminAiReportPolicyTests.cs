using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThreatIntelligence.API.Controllers;
using ThreatIntelligence.Domain.Entities;
using ThreatIntelligence.Infrastructure.AI;
using ThreatIntelligence.Application.AI;

namespace ThreatIntelligence.IntegrationTests;

[Collection("sqlserver")]
public sealed class AdminAiReportPolicyTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private static readonly string DefaultText =
        AiReportPolicySeeder.ReadDefaultReportText();

    public async Task InitializeAsync() => await fixture.ResetDataAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private AdminAiReportPolicyController CreateController()
    {
        var context = fixture.CreateContext();
        return new AdminAiReportPolicyController(context, TimeProvider.System);
    }

    private async Task SeedVersion1Async()
    {
        await using var context = fixture.CreateContext();
        var now = DateTime.UtcNow;
        context.AiReportPolicyVersions.Add(new AiReportPolicyVersion(1, DefaultText, null, now));
        context.AiReportPolicyActive.Add(new AiReportPolicyActive(1, now));
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task Get_returns_the_active_version_and_full_history()
    {
        await SeedVersion1Async();
        var controller = CreateController();
        await controller.Put(new AiReportPolicyPublishRequest("Sürüm iki metni", 1), CancellationToken.None);

        var result = await controller.Get(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<AiReportPolicyResponse>(ok.Value);
        Assert.Equal(2, response.ActiveVersion);
        Assert.Equal("Sürüm iki metni", response.Text);
        Assert.Equal(2, response.History.Count);
        Assert.Equal(1, response.History[0].Version);
        Assert.Equal(DefaultText, response.History[0].Text);
    }

    [Fact]
    public async Task Publish_appends_an_immutable_version_and_moves_the_active_pointer()
    {
        await SeedVersion1Async();
        var controller = CreateController();

        var result = await controller.Put(new AiReportPolicyPublishRequest("Yeni yönerge", 1), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<AiReportPolicyResponse>(ok.Value);
        Assert.Equal(2, response.ActiveVersion);

        await using var context = fixture.CreateContext();
        var versions = await context.AiReportPolicyVersions.OrderBy(version => version.Version).ToListAsync();
        Assert.Equal(2, versions.Count);
        Assert.Equal(DefaultText, versions[0].Text);
        Assert.Equal(1, await context.AiReportPolicyActive.CountAsync(active => active.ActiveVersion == 2));
    }

    [Fact]
    public async Task Stale_expected_version_conflicts_and_preserves_the_active_document()
    {
        await SeedVersion1Async();
        var controller = CreateController();
        await controller.Put(new AiReportPolicyPublishRequest("Sürüm iki", 1), CancellationToken.None);

        var conflict = await controller.Put(new AiReportPolicyPublishRequest("Geç sürüm", 1), CancellationToken.None);

        var conflictResult = Assert.IsType<ConflictObjectResult>(conflict.Result);
        Assert.Equal(409, conflictResult.StatusCode ?? StatusCodes.Status409Conflict);

        await using var context = fixture.CreateContext();
        var active = await context.AiReportPolicyActive.SingleAsync();
        Assert.Equal(2, active.ActiveVersion);
        Assert.Equal(2, await context.AiReportPolicyVersions.CountAsync());
    }

    [Fact]
    public async Task Restoring_a_previous_version_creates_a_new_version()
    {
        await SeedVersion1Async();
        var controller = CreateController();
        await controller.Put(new AiReportPolicyPublishRequest("Sürüm iki", 1), CancellationToken.None);

        var restore = await controller.Put(new AiReportPolicyPublishRequest(DefaultText, 2), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(restore.Result);
        var response = Assert.IsType<AiReportPolicyResponse>(ok.Value);
        Assert.Equal(3, response.ActiveVersion);
        Assert.Equal(DefaultText.Trim(), response.Text);

        await using var context = fixture.CreateContext();
        Assert.Equal(3, await context.AiReportPolicyVersions.CountAsync());
        Assert.Equal(DefaultText, (await context.AiReportPolicyVersions.SingleAsync(version => version.Version == 1)).Text);
        Assert.Equal(DefaultText.Trim(), (await context.AiReportPolicyVersions.SingleAsync(version => version.Version == 3)).Text);
    }

    [Fact]
    public async Task Blank_or_oversized_text_is_rejected_without_changes()
    {
        await SeedVersion1Async();
        var controller = CreateController();

        var blank = await controller.Put(new AiReportPolicyPublishRequest("   ", 1), CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(blank.Result);

        var oversized = await controller.Put(
            new AiReportPolicyPublishRequest(new string('a', 20001), 1), CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(oversized.Result);

        await using var context = fixture.CreateContext();
        Assert.Equal(1, await context.AiReportPolicyVersions.CountAsync());
        Assert.Equal(1, (await context.AiReportPolicyActive.SingleAsync()).ActiveVersion);
    }
}
