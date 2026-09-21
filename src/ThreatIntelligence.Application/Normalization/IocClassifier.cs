namespace ThreatIntelligence.Application.Normalization;

public interface IIocClassifier
{
    NormalizationOutput Classify(string query);
}

public class IocClassifier : IIocClassifier
{
    private readonly IIpv4Normalizer _ipv4Normalizer;
    private readonly IIpv6Normalizer _ipv6Normalizer;
    private readonly IUrlNormalizer _urlNormalizer;
    private readonly IDomainNormalizer _domainNormalizer;

    public IocClassifier(
        IIpv4Normalizer ipv4Normalizer,
        IIpv6Normalizer ipv6Normalizer,
        IUrlNormalizer urlNormalizer,
        IDomainNormalizer domainNormalizer)
    {
        _ipv4Normalizer = ipv4Normalizer;
        _ipv6Normalizer = ipv6Normalizer;
        _urlNormalizer = urlNormalizer;
        _domainNormalizer = domainNormalizer;
    }

    public NormalizationOutput Classify(string query)
    {
        var trimmed = query.Trim();
        if (trimmed.Length == 0)
        {
            return NormalizationOutput.Failure(
                NormalizationErrorCode.InvalidInput,
                ErrorMessages.InvalidInput);
        }

        var ipv4 = _ipv4Normalizer.Normalize(trimmed);
        if (ipv4.IsSuccess)
        {
            return ipv4;
        }

        var ipv6 = _ipv6Normalizer.Normalize(trimmed);
        if (ipv6.IsSuccess)
        {
            return ipv6;
        }

        if (trimmed.Contains("://"))
        {
            return _urlNormalizer.Normalize(trimmed);
        }

        var domain = _domainNormalizer.Normalize(trimmed);
        if (domain.IsSuccess)
        {
            return domain;
        }

        return NormalizationOutput.Failure(
            NormalizationErrorCode.InvalidInput,
            ErrorMessages.InvalidInput);
    }
}