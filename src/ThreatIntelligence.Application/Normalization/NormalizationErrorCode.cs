namespace ThreatIntelligence.Application.Normalization;

public enum NormalizationErrorCode : byte
{
    InvalidInput,
    LeadingZeros,
    MissingOctets,
    ExtraOctets,
    InvalidIpv4Port,
    InvalidIpv6,
    InvalidIpv6Port,
    UnmatchedBrackets,
    UnbracketedPort,
    InvalidDomain,
    DomainTooLong,
    LabelTooLong,
    EmptyLabel,
    TldOnly,
    InvalidUrlScheme,
    MissingUrlHost,
    UrlTooLong,
    InvalidPort,
    TypeMismatch,
    InvalidDomainCharacter,
    HyphenBoundary,
    DomainScheme,
    DomainPath,
    DomainPort,
    DomainWhitespace,
    NoRegistrableDomain,
    InvalidUrlHost,
    InvalidUrlCharacter
}
