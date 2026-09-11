using ThreatIntelligence.Application.Admin;

namespace ThreatIntelligence.Application.Abstractions.Persistence;

/// <summary>
/// Read model for the Admin grouped database summary. Counts distinct normalized
/// <c>(iocType, iocValue)</c> groups so cross-provider duplicates and port variants count once.
/// </summary>
public interface IAdminSummaryReader
{
    Task<AdminSummary> GetSummaryAsync(CancellationToken cancellationToken);
}
