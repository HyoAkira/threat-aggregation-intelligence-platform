using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThreatIntelligence.API.Controllers;
using ThreatIntelligence.Domain.Entities;

namespace ThreatIntelligence.IntegrationTests;

[Collection("sqlserver")]
public sealed class AdminSettingsControllerTests(SqlServerFixture fixture) : IAsyncLifetime
{
    public async Task InitializeAsync() => await fixture.ResetDataAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Read_returns_log_level_and_provider_controls()
    {
        await using var context = fixture.CreateContext();
        context.Settings.Add(new Settings("Logging:MinimumLevel", "Warning", DateTime.UtcNow));
        await context.SaveChangesAsync();
        var controller = new AdminSettingsController(context, TimeProvider.System);

        var result = await controller.Get(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<SettingsResponse>(ok.Value);
        Assert.Equal("Warning", response.LogLevel);
        Assert.Equal(2, response.Providers.Count);
    }

    [Fact]
    public async Task Valid_interval_change_recomputes_next_fetch_time()
    {
        await using var context = fixture.CreateContext();
        var provider = await context.Providers.SingleAsync(item => item.Id == 1);
        provider.MarkFetchSucceeded(DateTime.UtcNow.AddMinutes(-10));
        await context.SaveChangesAsync();
        var controller = new AdminSettingsController(context, TimeProvider.System);

        var result = await controller.Put(new SettingsRequest("Warning", [
            new ProviderSettingRequest(1, true, 15),
            new ProviderSettingRequest(2, true, 480)
        ]), CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        context.ChangeTracker.Clear();
        var updated = await context.Providers.SingleAsync(item => item.Id == 1);
        Assert.Equal(15, updated.IntervalMinutes);
        Assert.Equal(updated.LastFetchAt!.Value.AddMinutes(15), updated.NextFetchTime);
    }

    [Fact]
    public async Task Never_run_provider_keeps_null_schedule_after_settings_update()
    {
        await using var context = fixture.CreateContext();
        var controller = new AdminSettingsController(context, TimeProvider.System);

        var result = await controller.Put(new SettingsRequest("Information", [
            new ProviderSettingRequest(1, true, 10),
            new ProviderSettingRequest(2, true, 480)
        ]), CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        context.ChangeTracker.Clear();
        var provider = await context.Providers.SingleAsync(item => item.Id == 1);
        Assert.Equal(10, provider.IntervalMinutes);
        Assert.Null(provider.NextFetchTime);
    }

    [Fact]
    public async Task Invalid_request_writes_nothing()
    {
        await using var context = fixture.CreateContext();
        context.Settings.Add(new Settings("Logging:MinimumLevel", "Information", DateTime.UtcNow));
        await context.SaveChangesAsync();
        var controller = new AdminSettingsController(context, TimeProvider.System);

        var result = await controller.Put(new SettingsRequest("Error", [
            new ProviderSettingRequest(1, false, 5),
            new ProviderSettingRequest(2, false, 1)
        ]), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        context.ChangeTracker.Clear();
        var provider = await context.Providers.SingleAsync(item => item.Id == 1);
        var logSetting = await context.Settings.SingleAsync(item => item.Key == "Logging:MinimumLevel");
        Assert.True(provider.IsEnabled);
        Assert.Equal("Information", logSetting.Value);
    }
}
