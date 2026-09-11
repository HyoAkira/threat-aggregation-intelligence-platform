using ThreatIntelligence.Domain.Enums;

namespace ThreatIntelligence.Application.Normalization;

public interface IDomainNormalizer
{
    NormalizationOutput Normalize(string input);
}

public class DomainNormalizer : IDomainNormalizer
{
    private const int MaxDomainLength = 253;
    private const int MaxLabelLength = 63;

    private readonly IParentDomainResolver _parentDomainResolver;

    public DomainNormalizer(IParentDomainResolver parentDomainResolver)
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

        if (trimmed.Contains("://", StringComparison.Ordinal))
        {
            return NormalizationOutput.Failure(
                NormalizationErrorCode.DomainScheme,
                ErrorMessages.DomainScheme);
        }

        if (ContainsWhitespace(trimmed))
        {
            return NormalizationOutput.Failure(
                NormalizationErrorCode.DomainWhitespace,
                ErrorMessages.DomainWhitespace);
        }

        if (trimmed.Contains('/'))
        {
            return NormalizationOutput.Failure(
                NormalizationErrorCode.DomainPath,
                ErrorMessages.DomainPath);
        }

        if (trimmed.Contains(':'))
        {
            return NormalizationOutput.Failure(
                NormalizationErrorCode.DomainPort,
                ErrorMessages.DomainPort);
        }

        var lowered = trimmed.ToLowerInvariant();
        var domain = lowered.EndsWith('.') ? lowered[..^1] : lowered;

        if (domain.Length == 0)
        {
            return NormalizationOutput.Failure(
                NormalizationErrorCode.EmptyLabel,
                ErrorMessages.EmptyLabel);
        }

        if (domain.Length > MaxDomainLength)
        {
            return NormalizationOutput.Failure(
                NormalizationErrorCode.DomainTooLong,
                ErrorMessages.DomainTooLong);
        }

        if (!domain.Contains('.'))
        {
            return NormalizationOutput.Failure(
                NormalizationErrorCode.TldOnly,
                ErrorMessages.TldOnly);
        }

        if (!DomainLabelsAreValid(domain, out var failure))
        {
            return failure!;
        }

        var parentDomain = _parentDomainResolver.Resolve(domain);
        if (parentDomain is null)
        {
            return NormalizationOutput.Failure(
                NormalizationErrorCode.NoRegistrableDomain,
                ErrorMessages.NoRegistrableDomain);
        }

        return NormalizationOutput.Success(
            new NormalizationResult(IocType.Domain, domain, null, null, parentDomain));
    }

    private static bool ContainsWhitespace(string value)
    {
        foreach (var c in value)
        {
            if (char.IsWhiteSpace(c))
            {
                return true;
            }
        }

        return false;
    }

    private static bool DomainLabelsAreValid(string domain, out NormalizationOutput? failure)
    {
        var labels = domain.Split('.');
        for (var i = 0; i < labels.Length; i++)
        {
            var label = labels[i];
            if (label.Length == 0)
            {
                failure = NormalizationOutput.Failure(
                    NormalizationErrorCode.EmptyLabel,
                    ErrorMessages.EmptyLabel);
                return false;
            }

            if (label.Length > MaxLabelLength)
            {
                failure = NormalizationOutput.Failure(
                    NormalizationErrorCode.LabelTooLong,
                    ErrorMessages.LabelTooLong);
                return false;
            }

            if (label[0] == '-' || label[^1] == '-')
            {
                failure = NormalizationOutput.Failure(
                    NormalizationErrorCode.HyphenBoundary,
                    ErrorMessages.HyphenBoundary);
                return false;
            }

            for (var j = 0; j < label.Length; j++)
            {
                var c = label[j];
                if (!char.IsLetter(c) && !char.IsDigit(c) && c != '-')
                {
                    failure = NormalizationOutput.Failure(
                        NormalizationErrorCode.InvalidDomainCharacter,
                        ErrorMessages.InvalidDomainCharacter);
                    return false;
                }
            }
        }

        failure = null;
        return true;
    }
}
