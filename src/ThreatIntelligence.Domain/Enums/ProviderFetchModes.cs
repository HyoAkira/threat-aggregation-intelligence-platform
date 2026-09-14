namespace ThreatIntelligence.Domain.Enums;

/// <summary>
/// Bitmask of the fetch modes a provider exposes. Stored on the provider row so capabilities are
/// data-driven rather than derived from the provider name.
/// </summary>
[Flags]
public enum ProviderFetchModes : int
{
    None = 0,
    Recent = 1 << 0,
    Full = 1 << 1
}
