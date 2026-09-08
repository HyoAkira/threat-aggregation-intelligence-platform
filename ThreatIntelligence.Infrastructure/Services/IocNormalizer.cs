using System.Net;
using System.Security.Cryptography;
using System.Text;
using Nager.PublicSuffix;
using Nager.PublicSuffix.RuleProviders;
using ThreatIntelligence.Domain.Enums;

namespace ThreatIntelligence.Infrastructure.Services;

public class IocNormalizer
{
    private readonly DomainParser _domainParser;

    public IocNormalizer(string publicSuffixListPath)
    {
        var ruleProvider = new LocalFileRuleProvider(publicSuffixListPath);
        ruleProvider.BuildAsync().GetAwaiter().GetResult();
        _domainParser = new DomainParser(ruleProvider);
    }

    public NormalizedIoc Normalize(string value, IocType? expectedType = null)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException("IOC value is required.");
        }

        var trimmed = value.Trim();
        return expectedType switch
        {
            IocType.Url => NormalizeUrl(trimmed),
            IocType.Domain => NormalizeDomain(trimmed),
            IocType.Ipv4 => NormalizeIp(trimmed, IocType.Ipv4),
            IocType.Ipv6 => NormalizeIp(trimmed, IocType.Ipv6),
            _ => NormalizeDetected(trimmed)
        };
    }

    private NormalizedIoc NormalizeDetected(string value)
    {
        if (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("://", StringComparison.Ordinal))
        {
            return NormalizeUrl(value);
        }

        return TryParseIp(value, out var address, out var port)
            ? Create(address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? IocType.Ipv4 : IocType.Ipv6, address.ToString(), port, null, null)
            : NormalizeDomain(value);
    }

    private NormalizedIoc NormalizeDomain(string value)
    {
        var domain = value.EndsWith(".", StringComparison.Ordinal) ? value[..^1] : value;
        domain = domain.ToLowerInvariant();

        if (domain.Length > 253 || domain.Contains("//", StringComparison.Ordinal) ||
            domain.Contains('/') || domain.Contains(':') || domain.Any(char.IsWhiteSpace) ||
            !_domainParser.IsValidDomain(domain))
        {
            throw new InvalidDataException($"'{value}' is not a valid domain IOC.");
        }

        var info = _domainParser.Parse(domain);
        return Create(IocType.Domain, domain, null, null, info.RegistrableDomain);
    }

    private NormalizedIoc NormalizeUrl(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
            !string.IsNullOrEmpty(uri.UserInfo))
        {
            throw new InvalidDataException($"'{value}' is not a valid HTTP URL IOC.");
        }

        var fragmentIndex = value.IndexOf('#');
        var withoutFragment = fragmentIndex >= 0 ? value[..fragmentIndex] : value;
        var schemeSeparator = withoutFragment.IndexOf("://", StringComparison.Ordinal);
        var afterScheme = withoutFragment[(schemeSeparator + 3)..];
        var suffixIndex = afterScheme.IndexOfAny(['/', '?']);
        var authority = suffixIndex < 0 ? afterScheme : afterScheme[..suffixIndex];
        var suffix = suffixIndex < 0 ? string.Empty : afterScheme[suffixIndex..];
        var (host, port) = ParseUrlAuthority(authority);
        var normalizedHost = NormalizeUrlHost(host);
        var defaultPort = uri.Scheme == Uri.UriSchemeHttp ? 80 : 443;
        var renderedPort = port is null || port == defaultPort ? string.Empty : $":{port}";

        if (suffix == "/")
        {
            suffix = string.Empty;
        }
        else if (suffix.StartsWith("/?", StringComparison.Ordinal))
        {
            suffix = suffix[1..];
        }

        var parentDomain = IPAddress.TryParse(normalizedHost, out _)
            ? null
            : _domainParser.Parse(normalizedHost).RegistrableDomain;
        var normalized = $"{uri.Scheme.ToLowerInvariant()}://{normalizedHost}{renderedPort}{suffix}";

        if (normalized.Length > 2048)
        {
            throw new InvalidDataException("URL IOC exceeds 2048 characters.");
        }

        return Create(IocType.Url, normalized, port, normalizedHost, parentDomain);
    }

    private string NormalizeUrlHost(string host)
    {
        if (IPAddress.TryParse(host, out var address))
        {
            return address.ToString();
        }

        var normalized = host.ToLowerInvariant();
        if (!_domainParser.IsValidDomain(normalized))
        {
            throw new InvalidDataException($"'{host}' is not a valid URL host.");
        }

        return normalized;
    }

    private static (string Host, int? Port) ParseUrlAuthority(string authority)
    {
        if (string.IsNullOrWhiteSpace(authority) || authority.Contains('@'))
        {
            throw new InvalidDataException("URL IOC must contain a host and no user info.");
        }

        if (authority[0] == '[')
        {
            var closingBracket = authority.IndexOf(']');
            if (closingBracket < 0)
            {
                throw new InvalidDataException("IPv6 URL host is missing a closing bracket.");
            }

            var host = authority[1..closingBracket];
            var remainder = authority[(closingBracket + 1)..];
            return (host, ParsePort(remainder));
        }

        var separator = authority.LastIndexOf(':');
        if (separator < 0)
        {
            return (authority, null);
        }

        if (authority.IndexOf(':') != separator)
        {
            throw new InvalidDataException("IPv6 URL hosts must be enclosed in brackets.");
        }

        return (authority[..separator], ParsePort(authority[separator..]));
    }

    private static int? ParsePort(string suffix)
    {
        if (string.IsNullOrEmpty(suffix))
        {
            return null;
        }

        if (!suffix.StartsWith(':') || !int.TryParse(suffix[1..], out var port) || port is < 1 or > 65535)
        {
            throw new InvalidDataException("IOC port must be between 1 and 65535.");
        }

        return port;
    }

    private static NormalizedIoc NormalizeIp(string value, IocType expectedType)
    {
        if (!TryParseIp(value, out var address, out var port) ||
            (expectedType == IocType.Ipv4 && address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork) ||
            (expectedType == IocType.Ipv6 && address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetworkV6))
        {
            throw new InvalidDataException($"'{value}' is not a valid {expectedType} IOC.");
        }

        return Create(expectedType, address.ToString(), port, null, null);
    }

    private static bool TryParseIp(string value, out IPAddress address, out int? port)
    {
        address = IPAddress.None;
        port = null;

        if (value.StartsWith('['))
        {
            var closingBracket = value.IndexOf(']');
            if (closingBracket < 0 || !IPAddress.TryParse(value[1..closingBracket], out address) ||
                address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetworkV6)
            {
                return false;
            }

            try
            {
                port = ParsePort(value[(closingBracket + 1)..]);
                return true;
            }
            catch (InvalidDataException)
            {
                return false;
            }
        }

        if (IPAddress.TryParse(value, out address))
        {
            return address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork || address.ToString() == value;
        }

        var separator = value.LastIndexOf(':');
        if (separator < 1 || value.IndexOf(':') != separator ||
            !IPAddress.TryParse(value[..separator], out address) ||
            address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork || address.ToString() != value[..separator])
        {
            return false;
        }

        try
        {
            port = ParsePort(value[separator..]);
            return true;
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }

    private static NormalizedIoc Create(IocType type, string value, int? port, string? urlHost, string? parentDomain)
    {
        return new NormalizedIoc(type, value, SHA256.HashData(Encoding.UTF8.GetBytes(value)), port, urlHost, parentDomain);
    }
}
