using ThreatIntelligence.Domain.Enums;

namespace ThreatIntelligence.Application.Normalization;

public interface IUrlNormalizer
{
    NormalizationOutput Normalize(string input);
}

public class UrlNormalizer : IUrlNormalizer
{
    private const int MaxUrlLength = 2048;
    private const int MaxHostLength = 253;
    private const int MaxLabelLength = 63;

    private readonly IParentDomainResolver _parentDomainResolver;

    public UrlNormalizer(IParentDomainResolver parentDomainResolver)
    {
        _parentDomainResolver = parentDomainResolver;
    }

    public NormalizationOutput Normalize(string input)
    {
        var trimmed = input.Trim();
        if (trimmed.Length == 0)
        {
            return NormalizationOutput.Failure(
                NormalizationErrorCode.InvalidInput,
                ErrorMessages.InvalidInput);
        }

        var schemeEnd = trimmed.IndexOf("://", StringComparison.Ordinal);
        if (schemeEnd < 0)
        {
            return NormalizationOutput.Failure(
                NormalizationErrorCode.InvalidUrlScheme,
                ErrorMessages.InvalidUrlScheme);
        }

        var scheme = trimmed[..schemeEnd].ToLowerInvariant();
        if (scheme is not ("http" or "https"))
        {
            return NormalizationOutput.Failure(
                NormalizationErrorCode.InvalidUrlScheme,
                ErrorMessages.InvalidUrlScheme);
        }

        var isHttps = scheme == "https";
        var rest = trimmed[(schemeEnd + 3)..];
        var authorityEnd = FindAuthorityEnd(rest);
        var authority = authorityEnd < 0 ? rest : rest[..authorityEnd];
        var remainder = authorityEnd < 0 ? string.Empty : rest[authorityEnd..];

        var fragmentIndex = remainder.IndexOf('#');
        if (fragmentIndex >= 0)
        {
            remainder = remainder[..fragmentIndex];
        }

        var queryStart = remainder.IndexOf('?');
        string path;
        string query;
        if (queryStart >= 0)
        {
            query = remainder[queryStart..];
            path = remainder[..queryStart];
        }
        else
        {
            path = remainder;
            query = string.Empty;
        }

        if (ContainsWhitespaceOrControl(path) || ContainsWhitespaceOrControl(query))
        {
            return NormalizationOutput.Failure(
                NormalizationErrorCode.InvalidUrlCharacter,
                ErrorMessages.InvalidUrlCharacter);
        }

        if (authority.Length == 0)
        {
            return NormalizationOutput.Failure(
                NormalizationErrorCode.MissingUrlHost,
                ErrorMessages.MissingUrlHost);
        }

        if (authority.Contains('@'))
        {
            return NormalizationOutput.Failure(
                NormalizationErrorCode.InvalidUrlHost,
                ErrorMessages.InvalidUrlHost);
        }

        if (!TryParseAuthority(authority, out var hostPart, out var portText, out var hostIsIpLiteral, out var hostMissing))
        {
            return NormalizationOutput.Failure(
                NormalizationErrorCode.InvalidUrlHost,
                ErrorMessages.InvalidUrlHost);
        }

        if (hostMissing)
        {
            return NormalizationOutput.Failure(
                NormalizationErrorCode.MissingUrlHost,
                ErrorMessages.MissingUrlHost);
        }

        int? port = null;
        if (portText is not null)
        {
            if (!PortParser.TryParse(portText, out var parsedPort))
            {
                return NormalizationOutput.Failure(
                    NormalizationErrorCode.InvalidPort,
                    ErrorMessages.InvalidPort);
            }

            port = parsedPort;
        }

        var effectivePath = path switch
        {
            "" => string.Empty,
            "/" => string.Empty,
            _ => path
        };

        var hostPort = port is null
            ? hostPart
            : (isHttps ? port == 443 : port == 80)
                ? hostPart
                : hostPart + ":" + port;

        var normalized = $"{scheme}://{hostPort}{effectivePath}{query}";

        if (normalized.Length > MaxUrlLength)
        {
            return NormalizationOutput.Failure(
                NormalizationErrorCode.UrlTooLong,
                ErrorMessages.UrlTooLong);
        }

        string? parentDomain = null;
        if (!hostIsIpLiteral)
        {
            parentDomain = _parentDomainResolver.Resolve(hostPart);
        }

        return NormalizationOutput.Success(
            new NormalizationResult(IocType.Url, normalized, port, hostPart, parentDomain));
    }

    private static int FindAuthorityEnd(string rest)
    {
        var firstSlash = rest.IndexOf('/');
        var firstQuestion = rest.IndexOf('?');
        var firstHash = rest.IndexOf('#');

        var earliest = rest.Length;
        foreach (var index in new[] { firstSlash, firstQuestion, firstHash })
        {
            if (index >= 0 && index < earliest)
            {
                earliest = index;
            }
        }

        return earliest == rest.Length ? -1 : earliest;
    }

    private static bool TryParseAuthority(
        string authority,
        out string host,
        out string? portText,
        out bool hostIsIpLiteral,
        out bool hostMissing)
    {
        host = string.Empty;
        portText = null;
        hostIsIpLiteral = false;
        hostMissing = false;

        if (authority.StartsWith('['))
        {
            var closeIndex = authority.IndexOf(']');
            if (closeIndex < 0)
            {
                return false;
            }

            var addressText = authority[1..closeIndex];
            if (!Ipv6Parser.TryCanonicalize(addressText, out var canonical))
            {
                return false;
            }

            var afterBracket = authority[(closeIndex + 1)..];
            if (afterBracket.Length > 0)
            {
                if (!afterBracket.StartsWith(':'))
                {
                    return false;
                }

                portText = afterBracket[1..];
            }

            host = "[" + canonical + "]";
            hostIsIpLiteral = true;
            return true;
        }

        var colonIndex = authority.LastIndexOf(':');
        var hostText = authority;
        if (colonIndex >= 0)
        {
            if (authority[..colonIndex].Contains(':'))
            {
                return false;
            }

            hostText = authority[..colonIndex];
            portText = authority[(colonIndex + 1)..];
        }

        if (hostText.Length == 0)
        {
            hostMissing = true;
            return true;
        }

        if (Ipv4Parser.IsIpv4Shaped(hostText))
        {
            if (!Ipv4Parser.TryCanonicalize(hostText, out var canonicalV4))
            {
                return false;
            }

            host = canonicalV4;
            hostIsIpLiteral = true;
            return true;
        }

        if (!IsValidDnsHost(hostText))
        {
            return false;
        }

        host = hostText.ToLowerInvariant();
        hostIsIpLiteral = false;
        return true;
    }

    private static bool IsValidDnsHost(string host)
    {
        if (host.Length > MaxHostLength)
        {
            return false;
        }

        var labels = host.Split('.');
        foreach (var label in labels)
        {
            if (label.Length == 0 || label.Length > MaxLabelLength)
            {
                return false;
            }

            if (label[0] == '-' || label[^1] == '-')
            {
                return false;
            }

            foreach (var c in label)
            {
                if (!char.IsLetter(c) && !char.IsDigit(c) && c != '-')
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool ContainsWhitespaceOrControl(string value)
    {
        foreach (var c in value)
        {
            if (char.IsWhiteSpace(c) || char.IsControl(c))
            {
                return true;
            }
        }

        return false;
    }
}
