using Microsoft.EntityFrameworkCore;
using ThreatIntelligence.Application.AI;
using ThreatIntelligence.Application.Abstractions.Secrets;
using ThreatIntelligence.Infrastructure.Persistence;

namespace ThreatIntelligence.Infrastructure.AI;

/// <summary>
/// Reads the persisted AI connection settings and active report-policy version. Exposes only
/// whether the server API key is configured, never its value.
/// </summary>
public sealed class AiConfigurationReader(
    ThreatIntelligenceDbContext dbContext,
    ISecretStore secretStore) : IAiConfigurationReader
{
    public async Task<AiConnectionSnapshot?> GetConnectionAsync(CancellationToken cancellationToken)
    {
        var settings = await dbContext.AiConnectionSettings.AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
        if (settings is null)
        {
            return null;
        }

        var credential = await secretStore.GetStatusAsync(
            ManagedSecretNames.OpenAiCompatibleServerApiKey, cancellationToken);
        return new AiConnectionSnapshot(settings.BaseUrl, settings.ModelId, settings.Enabled, credential is not null);
    }

    public async Task<AiReportPolicySnapshot?> GetActivePolicyAsync(CancellationToken cancellationToken)
    {
        var pointer = await dbContext.AiReportPolicyActive.AsNoTracking()
            .Select(active => (int?)active.ActiveVersion)
            .SingleOrDefaultAsync(cancellationToken);
        if (pointer is null)
        {
            return null;
        }

        var version = await dbContext.AiReportPolicyVersions.AsNoTracking()
            .Where(item => item.Version == pointer.Value)
            .Select(item => new AiReportPolicySnapshot(item.Version, item.Text))
            .SingleOrDefaultAsync(cancellationToken);
        return version;
    }
}
