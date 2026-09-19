namespace ThreatIntelligence.Application.Features.Iocs.Queries;

/// <summary>One normalized tag and the number of provider-row associations using it.</summary>
public sealed record IocTagCatalogItem(string Name, int UsageCount);
