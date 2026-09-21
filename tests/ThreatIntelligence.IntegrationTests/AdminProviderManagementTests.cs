using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThreatIntelligence.API.Controllers;
using ThreatIntelligence.API.Errors;
using ThreatIntelligence.Application.Admin;
using ThreatIntelligence.Domain.Entities;
using ThreatIntelligence.Domain.Enums;
using ThreatIntelligence.Infrastructure.Admin;

namespace ThreatIntelligence.IntegrationTests;

[Collection("sqlserver")]
public sealed class AdminProviderManagementTests(SqlServerFixture fixture) : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await fixture.ResetDataAsync();
        await RemoveTestProvidersAsync();
    }

    public async Task DisposeAsync() => await RemoveTestProvidersAsync();

    [Fact]
    public async Task Create_returns_201_with_the_persisted_configuration()
    {
        var controller = CreateController();

        var result = await controller.Create(
            new ProviderWriteRequest(
                "AlienVault", "https://otx.example", 60, IsEnabled: true,
                DefaultMode: "recent", SupportedModes: ["recent", "full"],
                RecentCooldownMinutes: 10, FullCooldownMinutes: 120, MinimumIntervalMinutes: 30),
            CancellationToken.None);

        var created = Assert.IsType<CreatedResult>(result.Result);
        var item = Assert.IsType<AdminProviderDashboardItem>(created.Value);
        Assert.Equal("AlienVault", item.Name);
        Assert.Equal(["recent", "full"], item.SupportedModes);
        Assert.Equal("recent", item.DefaultMode);
        Assert.Equal(60, item.IntervalMinutes);

        var dashboard = await CreateController().Providers(CancellationToken.None);
        var providers = Assert.IsAssignableFrom<IReadOnlyList<AdminProviderDashboardItem>>(
            Assert.IsType<OkObjectResult>(dashboard.Result).Value);
        Assert.Contains(providers, provider => provider.Name == "AlienVault");
    }

    [Fact]
    public async Task Create_rejects_invalid_configuration()
    {
        var controller = CreateController();

        var unknownMode = await controller.Create(ValidRequest() with { SupportedModes = ["hourly"] }, CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(unknownMode.Result);

        var defaultNotSupported = await controller.Create(
            ValidRequest() with { DefaultMode = "full", SupportedModes = ["recent"] }, CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(defaultNotSupported.Result);

        var belowMinimum = await controller.Create(
            ValidRequest() with { IntervalMinutes = 5, MinimumIntervalMinutes = 30 }, CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(belowMinimum.Result);

        await using var context = fixture.CreateContext();
        Assert.False(await context.Providers.AnyAsync(provider => provider.Name == "AlienVault"));
    }

    [Fact]
    public async Task Create_rejects_a_duplicate_name()
    {
        var controller = CreateController();

        var result = await controller.Create(
            ValidRequest() with { Name = "ThreatFox" }, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        await using var context = fixture.CreateContext();
        Assert.Equal(2, await context.Providers.CountAsync());
    }

    [Fact]
    public async Task Update_changes_supported_modes_and_the_dashboard_reflects_them()
    {
        var controller = CreateController();

        var result = await controller.Update(
            2,
            new ProviderWriteRequest(
                "PhishTank", "https://data.phishtank.com", 480, IsEnabled: true,
                DefaultMode: "full", SupportedModes: ["recent", "full"],
                RecentCooldownMinutes: null, FullCooldownMinutes: 45, MinimumIntervalMinutes: 480),
            CancellationToken.None);

        var updated = Assert.IsType<AdminProviderDashboardItem>(
            Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(["recent", "full"], updated.SupportedModes);
        Assert.Equal("full", updated.DefaultMode);

        var dashboard = await CreateController().Providers(CancellationToken.None);
        var providers = Assert.IsAssignableFrom<IReadOnlyList<AdminProviderDashboardItem>>(
            Assert.IsType<OkObjectResult>(dashboard.Result).Value);
        var phishTank = providers.Single(provider => provider.Id == 2);
        Assert.Equal(["recent", "full"], phishTank.SupportedModes);
    }

    [Fact]
    public async Task Update_accepts_the_dashboard_payload_as_a_round_trip()
    {
        var readController = CreateController();
        var dashboard = await readController.Providers(CancellationToken.None);
        var providers = Assert.IsAssignableFrom<IReadOnlyList<AdminProviderDashboardItem>>(
            Assert.IsType<OkObjectResult>(dashboard.Result).Value);
        var threatFox = providers.Single(provider => provider.Id == 1);

        var controller = CreateController();
        var result = await controller.Update(
            threatFox.Id,
            new ProviderWriteRequest(
                threatFox.Name,
                threatFox.BaseUrl,
                threatFox.IntervalMinutes,
                threatFox.IsEnabled,
                threatFox.DefaultMode,
                threatFox.SupportedModes,
                threatFox.RecentCooldownMinutes,
                threatFox.FullCooldownMinutes,
                threatFox.MinimumIntervalMinutes),
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        var verify = await CreateController().Providers(CancellationToken.None);
        var persisted = Assert.IsAssignableFrom<IReadOnlyList<AdminProviderDashboardItem>>(
            Assert.IsType<OkObjectResult>(verify.Result).Value);
        var roundTripped = persisted.Single(provider => provider.Id == 1);
        Assert.Equal(["recent", "full"], roundTripped.SupportedModes);
        Assert.Equal(5, roundTripped.RecentCooldownMinutes);
        Assert.Equal(60, roundTripped.FullCooldownMinutes);
        Assert.Equal(5, roundTripped.MinimumIntervalMinutes);
    }

    [Fact]
    public async Task Update_unknown_provider_returns_404()
    {
        var controller = CreateController();

        var result = await controller.Update(999, ValidRequest(), CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task Delete_removes_an_unreferenced_provider()
    {
        var controller = CreateController();
        var created = Assert.IsType<AdminProviderDashboardItem>(
            Assert.IsType<CreatedResult>(
                (await controller.Create(ValidRequest(), CancellationToken.None)).Result).Value);

        var result = await controller.Delete(created.Id, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        await using var context = fixture.CreateContext();
        Assert.False(await context.Providers.AnyAsync(provider => provider.Id == created.Id));
    }

    [Fact]
    public async Task Delete_is_refused_for_a_provider_with_history()
    {
        var controller = CreateController();
        var created = Assert.IsType<AdminProviderDashboardItem>(
            Assert.IsType<CreatedResult>(
                (await controller.Create(ValidRequest(), CancellationToken.None)).Result).Value);

        await using (var context = fixture.CreateContext())
        {
            context.FetchHistory.Add(new FetchHistory(
                Guid.NewGuid(), created.Id, FetchTrigger.Manual, FetchMode.Recent, FetchStatus.Queued, DateTime.UtcNow));
            await context.SaveChangesAsync();
        }

        var result = await controller.Delete(created.Id, CancellationToken.None);

        var problem = Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal("provider_in_use", Assert.IsType<ApiProblemDetails>(problem.Value).Code);
        await using var verify = fixture.CreateContext();
        Assert.True(await verify.Providers.AnyAsync(provider => provider.Id == created.Id));
    }

    private static ProviderWriteRequest ValidRequest() => new(
        "AlienVault", "https://otx.example", 60, IsEnabled: true,
        DefaultMode: "recent", SupportedModes: ["recent", "full"],
        RecentCooldownMinutes: 10, FullCooldownMinutes: 120, MinimumIntervalMinutes: 30);

    private AdminProviderOperationsController CreateController() =>
        new(
            new AdminProviderOperationsReader(fixture.CreateContextFactory()),
            new AdminProviderRepository(fixture.CreateContextFactory()));

    private async Task RemoveTestProvidersAsync()
    {
        await using var context = fixture.CreateContext();
        await context.Database.ExecuteSqlRawAsync(
            "DELETE FROM fetchHistory WHERE providerId > 2; DELETE FROM providers WHERE id > 2;");
    }
}
