using System.Net;
using System.Net.Sockets;
using Nager.PublicSuffix;
using Nager.PublicSuffix.RuleProviders;
using ThreatIntelligence.Application.Normalization;

namespace ThreatIntelligence.Infrastructure.PublicSuffix;

public class PublicSuffixParentDomainResolver : IParentDomainResolver
{
    private readonly DomainParser _parser;

    public PublicSuffixParentDomainResolver()
    {
        var dataPath = Path.Combine(AppContext.BaseDirectory, "Data", "public_suffix_list.dat");
        var ruleProvider = new LocalFileRuleProvider(dataPath);
        ruleProvider.BuildAsync().GetAwaiter().GetResult();
        _parser = new DomainParser(ruleProvider);
    }

    public string? Resolve(string host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return null;
        }

        var trimmed = host.Trim().TrimEnd('.');
        if (IsIpAddress(trimmed) || IsSingleLabel(trimmed))
        {
            return trimmed;
        }

        if (_parser.TryParse(trimmed, out var info))
        {
            return info.RegistrableDomain is { Length: > 0 } registrable
                ? registrable
                : null;
        }

        if (trimmed.Length <= 64)
        {
            var probeLabel = "r." + trimmed;
            if (_parser.TryParse(probeLabel, out var subInfo) &&
                subInfo.RegistrableDomain is { Length: > 0 })
            {
                return null;
            }
        }

        string[] labels = trimmed.Split('.');
        if (labels.Length < 2)
        {
            return null;
        }

        return labels[^2] + "." + labels[^1];
    }

    private static bool IsIpAddress(string host)
        => IPAddress.TryParse(host, out var parsed) &&
           parsed.AddressFamily is AddressFamily.InterNetwork or AddressFamily.InterNetworkV6;

    private static bool IsSingleLabel(string host)
        => !host.Contains('.');
}
