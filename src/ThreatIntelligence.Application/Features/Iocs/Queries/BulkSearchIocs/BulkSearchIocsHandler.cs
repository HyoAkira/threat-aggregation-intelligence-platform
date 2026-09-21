using MediatR;
using ThreatIntelligence.Application.Abstractions.Persistence;
using ThreatIntelligence.Application.Normalization;

namespace ThreatIntelligence.Application.Features.Iocs.Queries.BulkSearchIocs;

/// <summary>
/// Evaluates a bounded list of IOC values as one request. Blank entries are removed, the remaining
/// values are trimmed, classified, and deduplicated for shared reader work, and the original order
/// is preserved in the response. A per-value invalid or absent outcome never fails the whole request.
/// </summary>
public sealed class BulkSearchIocsHandler(
    IIocClassifier classifier,
    IIocQueryReader reader) : IRequestHandler<BulkSearchIocsQuery, BulkSearchIocsResult>
{
    public async Task<BulkSearchIocsResult> Handle(
        BulkSearchIocsQuery request,
        CancellationToken cancellationToken)
    {
        var inputs = NormalizeInputs(request.Queries);
        var (page, pageSize) = IocSearchEvaluator.ResolvePageBounds(request.Page, request.PageSize);
        var provider = IocSearchEvaluator.NormalizeProvider(request.Provider);

        var identities = new List<NormalizationResult?>(inputs.Count);
        var errors = new List<BulkSearchProblem?>(inputs.Count);
        foreach (var input in inputs)
        {
            if (IocSearchEvaluator.TryClassify(classifier, input, out var identity, out var detail))
            {
                identities.Add(identity);
                errors.Add(null);
            }
            else
            {
                identities.Add(null);
                errors.Add(new BulkSearchProblem(IocErrorCodes.InvalidInput, detail));
            }
        }

        var distinct = identities
            .Where(identity => identity is not null)
            .Select(identity => identity!)
            .Distinct()
            .ToList();

        var resolved = distinct.Count == 0
            ? new Dictionary<NormalizationResult, PagedResult<IocGroupResult>>()
            : await reader.SearchBatchAsync(
                distinct,
                new IocSourceFilter(request.Tag, provider),
                page,
                pageSize,
                cancellationToken);

        var items = new List<BulkSearchIocItem>(inputs.Count);
        var matched = 0;
        var notFound = 0;
        var invalid = 0;
        for (var index = 0; index < inputs.Count; index++)
        {
            var identity = identities[index];
            if (identity is null)
            {
                invalid++;
                items.Add(new BulkSearchIocItem(
                    inputs[index],
                    BulkSearchStatus.Invalid,
                    [],
                    page,
                    pageSize,
                    0,
                    errors[index]));
                continue;
            }

            var paged = resolved.TryGetValue(identity, out var match)
                ? match
                : PagedResult<IocGroupResult>.Empty(page, pageSize);
            if (paged.TotalCount == 0)
            {
                notFound++;
                items.Add(new BulkSearchIocItem(
                    inputs[index],
                    BulkSearchStatus.NotFound,
                    [],
                    page,
                    pageSize,
                    0,
                    new BulkSearchProblem(
                        IocErrorCodes.NotFound,
                        $"No {IocTypeToken.ToToken(identity.Type)} IOC matches '{identity.IocValue}'.")));
                continue;
            }

            matched++;
            items.Add(new BulkSearchIocItem(
                inputs[index],
                BulkSearchStatus.Matched,
                paged.Items,
                paged.Page,
                paged.PageSize,
                paged.TotalCount,
                null));
        }

        return new BulkSearchIocsResult(items, inputs.Count, matched, notFound, invalid);
    }

    private static IReadOnlyList<string> NormalizeInputs(IReadOnlyList<string>? queries)
    {
        if (queries is null)
        {
            throw new IocQueryValidationException(
                IocErrorCodes.InvalidInput,
                "The queries field is required.");
        }

        var inputs = new List<string>(queries.Count);
        foreach (var raw in queries)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            var trimmed = raw.Trim();
            if (trimmed.Length > BulkSearchLimits.MaxQueryLength)
            {
                throw new IocQueryValidationException(
                    IocErrorCodes.InvalidInput,
                    $"Each IOC value must be no longer than {BulkSearchLimits.MaxQueryLength} characters.");
            }

            inputs.Add(trimmed);
        }

        if (inputs.Count == 0)
        {
            throw new IocQueryValidationException(
                IocErrorCodes.InvalidInput,
                "At least one non-empty IOC value is required.");
        }

        if (inputs.Count > BulkSearchLimits.MaxQueries)
        {
            throw new IocQueryValidationException(
                IocErrorCodes.InvalidInput,
                $"A bulk search may contain at most {BulkSearchLimits.MaxQueries} non-empty IOC values.");
        }

        return inputs;
    }
}
