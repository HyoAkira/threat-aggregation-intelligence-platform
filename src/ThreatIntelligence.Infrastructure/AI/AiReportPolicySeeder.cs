using Microsoft.EntityFrameworkCore;
using ThreatIntelligence.Domain.Entities;
using ThreatIntelligence.Infrastructure.Persistence;

namespace ThreatIntelligence.Infrastructure.AI;

/// <summary>
/// Seeds the default Turkish report instruction document as immutable version 1 with an active
/// pointer, but only while no policy version exists. Repeated startup and later Admin versions
/// are preserved untouched.
/// </summary>
public sealed class AiReportPolicySeeder(
    ThreatIntelligenceDbContext dbContext,
    TimeProvider timeProvider)
{
    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        var exists = await dbContext.AiReportPolicyVersions.AsNoTracking().AnyAsync(cancellationToken);
        if (exists)
        {
            return;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        dbContext.AiReportPolicyVersions.Add(new AiReportPolicyVersion(1, ReadDefaultReportText(), null, now));
        dbContext.AiReportPolicyActive.Add(new AiReportPolicyActive(1, now));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public static string ReadDefaultReportText()
    {
        var assembly = typeof(AiReportPolicySeeder).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .Single(name => name.EndsWith("default-report.md", StringComparison.OrdinalIgnoreCase));
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException("The default report policy resource is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
