namespace ThreatIntelligence.Application.Features.Iocs.Queries;

/// <summary>One normalized IOC value and its lowercase IOC type token for prefix suggestions.</summary>
public sealed record IocSuggestionItem(string Value, string IocType);

/// <summary>Bounds shared by the suggestion endpoint and its reader operation.</summary>
public static class IocSuggestionLimits
{
    public const int DefaultLimit = 8;
    public const int MinLimit = 1;
    public const int MaxLimit = 20;
    public const int MaxPrefixLength = 2048;
}
