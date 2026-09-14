namespace ThreatIntelligence.Domain.Enums;

public static class FetchModeExtensions
{
    public static ProviderFetchModes ToProviderMode(this FetchMode mode) =>
        (ProviderFetchModes)(1 << (int)mode);
}
