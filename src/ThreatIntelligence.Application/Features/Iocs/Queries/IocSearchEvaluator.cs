using ThreatIntelligence.Application.Normalization;

namespace ThreatIntelligence.Application.Features.Iocs.Queries;

/// <summary>
/// Single source of truth for the IOC search request evaluation shared by the exact-search and
/// bulk-search handlers: page-bound validation, provider-filter normalization, and
/// classification/normalization of an input value. Keeping these rules here prevents the single and
/// bulk paths from drifting.
/// </summary>
public static class IocSearchEvaluator
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    /// <summary>Stored malware column capacity; filters longer than this are rejected.</summary>
    public const int MaxMalwareFilterLength = 200;

    /// <summary>Stored target column capacity; filters longer than this are rejected.</summary>
    public const int MaxTargetFilterLength = 500;

    /// <summary>Classifies a query, throwing the shared invalid-input validation failure.</summary>
    public static NormalizationResult RequireClassification(IIocClassifier classifier, string? query)
    {
        var classification = classifier.Classify(query ?? string.Empty);
        if (!classification.IsSuccess)
        {
            throw new IocQueryValidationException(
                IocErrorCodes.InvalidInput,
                classification.Error!.Message);
        }

        return classification.OrThrow();
    }

    /// <summary>
    /// Non-throwing classification used by bulk search: an unclassifiable value becomes a per-item
    /// invalid outcome instead of aborting the whole request.
    /// </summary>
    public static bool TryClassify(
        IIocClassifier classifier,
        string query,
        out NormalizationResult identity,
        out string errorDetail)
    {
        var classification = classifier.Classify(query);
        if (classification.IsSuccess)
        {
            identity = classification.Result!;
            errorDetail = string.Empty;
            return true;
        }

        identity = null!;
        errorDetail = classification.Error?.Message ?? ErrorMessages.InvalidInput;
        return false;
    }

    /// <summary>Applies the shared page defaults and inclusive bounds.</summary>
    public static (int Page, int PageSize) ResolvePageBounds(int? page, int? pageSize)
    {
        var resolvedPage = page ?? 1;
        var resolvedPageSize = pageSize ?? DefaultPageSize;
        if (resolvedPage < 1)
        {
            throw new IocQueryValidationException(
                IocErrorCodes.InvalidInput,
                "The page must be at least 1.");
        }

        if (resolvedPageSize is < 1 or > MaxPageSize)
        {
            throw new IocQueryValidationException(
                IocErrorCodes.InvalidInput,
                $"The pageSize must be between 1 and {MaxPageSize}.");
        }

        return (resolvedPage, resolvedPageSize);
    }

    /// <summary>Trims the optional provider filter and rejects an overlong value.</summary>
    public static string? NormalizeProvider(string? provider)
    {
        if (string.IsNullOrWhiteSpace(provider))
        {
            return null;
        }

        var trimmed = provider.Trim();
        if (trimmed.Length > 255)
        {
            throw new IocQueryValidationException(
                IocErrorCodes.InvalidInput,
                "The provider filter is too long.");
        }

        return trimmed;
    }

    /// <summary>
    /// Trims the optional malware filter, treats blank as absent, and rejects values beyond the
    /// stored column capacity. Comparison itself is case-insensitive and handled by the reader.
    /// </summary>
    public static string? NormalizeMalware(string? malware) =>
        NormalizeMetadataFilter(malware, MaxMalwareFilterLength, "malware");

    /// <summary>
    /// Trims the optional target filter, treats blank as absent, and rejects values beyond the
    /// stored column capacity. Comparison itself is case-insensitive and handled by the reader.
    /// </summary>
    public static string? NormalizeTarget(string? target) =>
        NormalizeMetadataFilter(target, MaxTargetFilterLength, "target");

    private static string? NormalizeMetadataFilter(string? value, int maxLength, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new IocQueryValidationException(
                IocErrorCodes.InvalidInput,
                $"The {name} filter must be no longer than {maxLength} characters.");
        }

        return trimmed;
    }
}
