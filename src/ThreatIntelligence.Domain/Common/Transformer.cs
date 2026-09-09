using ThreatIntelligence.Domain.ValueObjects;

namespace ThreatIntelligence.Domain.Common;

public record NormalizedUrl(string value, string urlHost, string parentDomain);

public static class Transformer
{

    public static NormalizedDomain NormalizeDomain(string value)
    {
        return new NormalizedDomain(value.Trim().ToLower(), string.Empty);
    }

    public static string NormalizeTag(string value)
    {

        return value.Trim().ToLower();
    }


    public static NormalizedUrl NormalizeUrl(string value)
    {

        return new NormalizedUrl(value.Trim().ToLower(), string.Empty, string.Empty);
    }
}