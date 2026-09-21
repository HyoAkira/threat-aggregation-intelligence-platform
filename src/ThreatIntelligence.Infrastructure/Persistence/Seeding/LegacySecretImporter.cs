using Microsoft.Extensions.Logging;
using ThreatIntelligence.Application.Abstractions.Secrets;
using ThreatIntelligence.Infrastructure.Configuration;

namespace ThreatIntelligence.Infrastructure.Persistence.Seeding;

public sealed class LegacySecretImporter(
    ThreatFoxOptions threatFoxOptions,
    PhishTankOptions phishTankOptions,
    ISecretStore secretStore,
    ILogger<LegacySecretImporter>? logger = null)
{
    private readonly ILogger<LegacySecretImporter> _logger = logger ??
        Microsoft.Extensions.Logging.Abstractions.NullLogger<LegacySecretImporter>.Instance;

    public async Task ImportAsync(CancellationToken cancellationToken)
    {
        await ImportOneAsync(ManagedSecretNames.ThreatFoxApiKey, threatFoxOptions.ApiKey, cancellationToken);
        await ImportOneAsync(ManagedSecretNames.PhishTankAppKey, phishTankOptions.AppKey, cancellationToken);
    }

    private async Task ImportOneAsync(string name, string? value, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        if (await secretStore.GetStatusAsync(name, cancellationToken) is not null)
        {
            _logger.LogInformation("Legacy credential import skipped Identity={Identity} Reason=AlreadyConfigured", name);
            return;
        }

        var result = await secretStore.SetAsync(name, value, cancellationToken);
        _logger.LogInformation("Legacy credential import completed Identity={Identity} Status={Status}", name, result.Status);
    }
}
