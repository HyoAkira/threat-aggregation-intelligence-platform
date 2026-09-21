using Microsoft.EntityFrameworkCore;
using ThreatIntelligence.Application.Abstractions.Persistence;
using ThreatIntelligence.Application.Admin;
using ThreatIntelligence.Domain.Enums;
using ThreatIntelligence.Infrastructure.Persistence;

namespace ThreatIntelligence.Infrastructure.Admin;

/// <summary>
/// Counts distinct normalized <c>(iocType, iocValue)</c> groups in one grouped query, then tallies
/// the per-type totals in memory. Cross-provider duplicates and port variants collapse to one group
/// because the group key excludes provider and port.
/// </summary>
public sealed class AdminSummaryReader(ThreatIntelligenceDbContext dbContext) : IAdminSummaryReader
{
    public async Task<AdminSummary> GetSummaryAsync(CancellationToken cancellationToken)
    {
        var groupTypes = await dbContext.Iocs.AsNoTracking()
            .GroupBy(ioc => new { ioc.IocType, ioc.IocValue })
            .Select(group => group.Key.IocType)
            .ToListAsync(cancellationToken);

        var ipv4 = 0;
        var ipv6 = 0;
        var domain = 0;
        var url = 0;
        foreach (var type in groupTypes)
        {
            switch (type)
            {
                case IocType.Ipv4:
                    ipv4++;
                    break;
                case IocType.Ipv6:
                    ipv6++;
                    break;
                case IocType.Domain:
                    domain++;
                    break;
                case IocType.Url:
                    url++;
                    break;
            }
        }

        return new AdminSummary(groupTypes.Count, ipv4, ipv6, domain, url);
    }
}
