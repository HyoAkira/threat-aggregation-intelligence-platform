namespace ThreatIntelligence.Application.Normalization;

public static class ErrorMessages
{
    public const string InvalidInput = "The input does not match any supported IOC type.";
    public const string LeadingZeros = "IPv4 octet contains ambiguous leading zeros.";
    public const string MissingOctets = "IPv4 address has fewer than four octets.";
    public const string ExtraOctets = "IPv4 address has more than four octets.";
    public const string InvalidIpv4Port = "IPv4 port must be between 1 and 65535.";
    public const string InvalidIpv6 = "The input is not a valid IPv6 address.";
    public const string InvalidIpv6Port = "IPv6 port must be between 1 and 65535.";
    public const string UnmatchedBrackets = "IPv6 address has unmatched brackets.";
    public const string UnbracketedPort = "IPv6 port requires bracketed address syntax.";
    public const string InvalidDomain = "The input is not a valid domain name.";
    public const string DomainTooLong = "Domain exceeds the 253-character limit.";
    public const string LabelTooLong = "Domain label exceeds the 63-character limit.";
    public const string EmptyLabel = "Domain contains an empty label.";
    public const string TldOnly = "Domain must not be a top-level domain only.";
    public const string InvalidUrlScheme = "URL must use http or https scheme.";
    public const string MissingUrlHost = "URL is missing a host.";
    public const string UrlTooLong = "Normalized URL exceeds the 2048-character limit.";
    public const string InvalidPort = "Port must be between 1 and 65535.";
    public const string TypeMismatch = "Declared IOC type does not match the normalized type.";
    public const string InvalidDomainCharacter = "Domain label contains invalid characters.";
    public const string HyphenBoundary = "Domain label must not begin or end with a hyphen.";
    public const string DomainScheme = "Domain must not contain a scheme.";
    public const string DomainPath = "Domain must not contain a path.";
    public const string DomainPort = "Domain must not contain a port.";
    public const string DomainWhitespace = "Domain must not contain whitespace.";
    public const string NoRegistrableDomain = "Domain must not be a public suffix only.";
    public const string InvalidUrlHost = "URL host is not valid.";
    public const string InvalidUrlCharacter = "URL path or query contains invalid whitespace or control characters.";
}
