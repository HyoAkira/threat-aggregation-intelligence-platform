using ThreatIntelligence.Application.Normalization;
using ThreatIntelligence.Application.ProviderRecords;
using ThreatIntelligence.Domain.Enums;
using ThreatIntelligence.Infrastructure.PublicSuffix;

namespace ThreatIntelligence.UnitTests.ProviderRecords;

internal sealed class AcceptAllValidator : IProviderValidator
{
    public const int ValidatorProviderId = 1;

    public int ProviderId => ValidatorProviderId;

    public ProviderRejection? Validate(ProviderRecordInput input) => null;
}

internal static class ProviderValidationFactory
{
    public static ProviderRecordValidator CreateValidator(params IProviderValidator[] validators)
    {
        var resolver = new PublicSuffixParentDomainResolver();
        var classifier = new IocClassifier(
            new Ipv4Normalizer(),
            new Ipv6Normalizer(),
            new UrlNormalizer(resolver),
            new DomainNormalizer(resolver));
        return new ProviderRecordValidator(
            classifier,
            validators.ToDictionary(v => v.ProviderId));
    }

    public static ProviderRecordValidator CreateValidatorWithDefault()
        => CreateValidator(new AcceptAllValidator());

    public static ProviderRecordInput ValidInput() => new(
        ProviderId: 1,
        ProviderRecordId: "ioc-123",
        RawIoc: "Api.Example.CO.UK.",
        DeclaredType: IocType.Domain,
        FirstSeenAt: new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc),
        LastSeenAt: new DateTime(2026, 9, 7, 10, 30, 0, DateTimeKind.Utc),
        ProviderDetailsLink: "https://threatfox.example/ioc123",
        Tags: new[] { " BotNet ", "botnet " },
        Metadata: null,
        Payload: new Dictionary<string, object?> { ["malware"] = "trickbot" });
}