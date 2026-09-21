using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThreatIntelligence.API.Controllers;
using ThreatIntelligence.Domain.Entities;

namespace ThreatIntelligence.IntegrationTests;

[Collection("sqlserver")]
public sealed class AdminAiSettingsControllerTests(SqlServerFixture fixture) : IAsyncLifetime
{
    public async Task InitializeAsync() => await fixture.ResetDataAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Get_returns_defaults_and_credential_status()
    {
        await using var context = fixture.CreateContext();
        var controller = new AdminAiSettingsController(context, fixture.SecretStore, TimeProvider.System);

        var result = await controller.Get(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<AiSettingsResponse>(ok.Value);
        Assert.Null(response.BaseUrl);
        Assert.Null(response.ModelId);
        Assert.False(response.Enabled);
        Assert.False(response.ApiKeyConfigured);
    }

    [Fact]
    public async Task Valid_save_persists_settings_and_reports_credential_status()
    {
        await using var context = fixture.CreateContext();
        await context.EncryptedSecrets.AddAsync(CreateSecret());
        await context.SaveChangesAsync();
        var controller = new AdminAiSettingsController(context, fixture.SecretStore, TimeProvider.System);

        var result = await controller.Put(new AiSettingsRequest("http://ollama:11434", "llama3.1", true),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<AiSettingsResponse>(ok.Value);
        Assert.Equal("http://ollama:11434", response.BaseUrl);
        Assert.Equal("llama3.1", response.ModelId);
        Assert.True(response.Enabled);
        Assert.True(response.ApiKeyConfigured);

        context.ChangeTracker.Clear();
        var stored = await context.AiConnectionSettings.SingleAsync();
        Assert.True(stored.Enabled);
        Assert.Equal("http://ollama:11434", stored.BaseUrl);
    }

    [Fact]
    public async Task Enabled_requires_base_url_and_model_id()
    {
        await using var context = fixture.CreateContext();
        var controller = new AdminAiSettingsController(context, fixture.SecretStore, TimeProvider.System);

        var missingModel = await controller.Put(new AiSettingsRequest("http://ollama:11434", null, true), CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(missingModel.Result);

        var missingUrl = await controller.Put(new AiSettingsRequest(null, "llama3.1", true), CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(missingUrl.Result);

        var relativeUrl = await controller.Put(new AiSettingsRequest("ollama:11434", "llama3.1", true), CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(relativeUrl.Result);

        var ftpUrl = await controller.Put(new AiSettingsRequest("ftp://ollama:11434", "llama3.1", true), CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(ftpUrl.Result);

        context.ChangeTracker.Clear();
        Assert.False(await context.AiConnectionSettings.AnyAsync());
    }

    [Fact]
    public async Task Invalid_save_rolls_back_to_previous_settings()
    {
        await using var context = fixture.CreateContext();
        var controller = new AdminAiSettingsController(context, fixture.SecretStore, TimeProvider.System);
        await controller.Put(new AiSettingsRequest("http://ollama:11434", "llama3.1", true), CancellationToken.None);

        var invalid = await controller.Put(new AiSettingsRequest("http://other:8080", "qwen", false), CancellationToken.None);
        Assert.IsType<OkObjectResult>(invalid.Result);

        var rejected = await controller.Put(new AiSettingsRequest("not-a-url", "qwen", true), CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(rejected.Result);

        context.ChangeTracker.Clear();
        var stored = await context.AiConnectionSettings.SingleAsync();
        Assert.Equal("http://other:8080", stored.BaseUrl);
        Assert.Equal("qwen", stored.ModelId);
        Assert.False(stored.Enabled);
    }

    [Fact]
    public async Task Disabled_save_allows_a_partial_draft()
    {
        await using var context = fixture.CreateContext();
        var controller = new AdminAiSettingsController(context, fixture.SecretStore, TimeProvider.System);

        var result = await controller.Put(new AiSettingsRequest(null, null, false), CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        context.ChangeTracker.Clear();
        var stored = await context.AiConnectionSettings.SingleAsync();
        Assert.Null(stored.BaseUrl);
        Assert.Null(stored.ModelId);
        Assert.False(stored.Enabled);
    }

    private static EncryptedSecret CreateSecret()
    {
        var rootKey = Enumerable.Range(1, 32).Select(index => (byte)index).ToArray();
        var protector = new Infrastructure.Secrets.AesGcmSecretProtector(
            new Infrastructure.Configuration.EncryptionOptions(rootKey, 1, null));
        var payload = protector.Protect(
            Application.Abstractions.Secrets.ManagedSecretNames.OpenAiCompatibleServerApiKey, "test-key");
        return new EncryptedSecret(
            Application.Abstractions.Secrets.ManagedSecretNames.OpenAiCompatibleServerApiKey,
            payload.FormatVersion, payload.KeyVersion, payload.Ciphertext, payload.Nonce,
            payload.AuthenticationTag, DateTime.UtcNow, DateTime.UtcNow);
    }
}
