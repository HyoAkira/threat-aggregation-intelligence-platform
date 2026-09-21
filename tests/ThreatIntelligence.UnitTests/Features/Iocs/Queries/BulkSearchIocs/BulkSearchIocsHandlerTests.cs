using ThreatIntelligence.Application.Abstractions.Persistence;
using ThreatIntelligence.Application.Features.Iocs.Queries;
using ThreatIntelligence.Application.Features.Iocs.Queries.BulkSearchIocs;
using ThreatIntelligence.Application.Normalization;
using ThreatIntelligence.Domain.Enums;

namespace ThreatIntelligence.UnitTests.Features.Iocs.Queries.BulkSearchIocs;

public sealed class BulkSearchIocsHandlerTests
{
    private static readonly IocGroupResult Group = new(
        1,
        "1.2.3.4",
        "ipv4",
        null,
        null,
        DateTime.UnixEpoch,
        null,
        []);

    private static readonly IReadOnlyDictionary<string, NormalizationResult> EmptyMap =
        new Dictionary<string, NormalizationResult>();

    private static readonly IReadOnlyDictionary<NormalizationResult, PagedResult<IocGroupResult>> EmptyResults =
        new Dictionary<NormalizationResult, PagedResult<IocGroupResult>>();

    private static NormalizationResult Ip(string value) =>
        new(IocType.Ipv4, value, null, null, null);

    private static NormalizationResult Domain(string value) =>
        new(IocType.Domain, value, null, null, value);

    [Fact]
    public async Task Handle_MixedInputs_ProducesOrderedStatusesAndSummaryCounts()
    {
        var reader = new StubReader(new Dictionary<NormalizationResult, PagedResult<IocGroupResult>>
        {
            [Ip("1.2.3.4")] = new([Group], 1, 20, 1),
            [Domain("absent.example")] = PagedResult<IocGroupResult>.Empty(1, 20),
        });
        var classifier = new MapClassifier(new Dictionary<string, NormalizationResult>
        {
            ["1.2.3.4"] = Ip("1.2.3.4"),
            ["absent.example"] = Domain("absent.example"),
        });
        var handler = new BulkSearchIocsHandler(classifier, reader);

        var result = await handler.Handle(
            new BulkSearchIocsQuery(["1.2.3.4", "absent.example", "not an ioc"]),
            CancellationToken.None);

        Assert.Equal(3, result.TotalCount);
        Assert.Equal(1, result.MatchedCount);
        Assert.Equal(1, result.NotFoundCount);
        Assert.Equal(1, result.InvalidCount);
        Assert.Equal(
            [BulkSearchStatus.Matched, BulkSearchStatus.NotFound, BulkSearchStatus.Invalid],
            result.Items.Select(item => item.Status).ToArray());
        Assert.Equal(["1.2.3.4", "absent.example", "not an ioc"], result.Items.Select(item => item.Input).ToArray());

        var matched = result.Items[0];
        Assert.Same(Group, Assert.Single(matched.Groups));
        Assert.Null(matched.Error);
        Assert.Equal(1, matched.TotalCount);

        var notFound = result.Items[1];
        Assert.Empty(notFound.Groups);
        Assert.Equal(IocErrorCodes.NotFound, notFound.Error!.Code);

        var invalid = result.Items[2];
        Assert.Empty(invalid.Groups);
        Assert.Equal(IocErrorCodes.InvalidInput, invalid.Error!.Code);
    }

    [Fact]
    public async Task Handle_DuplicateLinesRetainPositionsAndShareOneReaderCall()
    {
        var reader = new StubReader(new Dictionary<NormalizationResult, PagedResult<IocGroupResult>>
        {
            [Ip("1.2.3.4")] = new([Group], 1, 20, 1),
            [Domain("absent.example")] = PagedResult<IocGroupResult>.Empty(1, 20),
        });
        var classifier = new MapClassifier(new Dictionary<string, NormalizationResult>
        {
            ["1.2.3.4"] = Ip("1.2.3.4"),
            ["absent.example"] = Domain("absent.example"),
        });
        var handler = new BulkSearchIocsHandler(classifier, reader);

        var result = await handler.Handle(
            new BulkSearchIocsQuery(["1.2.3.4", "absent.example", "1.2.3.4"]),
            CancellationToken.None);

        Assert.Equal(3, result.Items.Count);
        Assert.Equal(BulkSearchStatus.Matched, result.Items[0].Status);
        Assert.Equal(BulkSearchStatus.NotFound, result.Items[1].Status);
        Assert.Equal(BulkSearchStatus.Matched, result.Items[2].Status);
        // Three inputs, but the duplicate shares one identity so the reader sees two.
        Assert.Equal(2, reader.Identities.Count);
    }

    [Fact]
    public async Task Handle_BlankEntriesAreRemovedAndValuesTrimmed()
    {
        var reader = new StubReader(new Dictionary<NormalizationResult, PagedResult<IocGroupResult>>
        {
            [Ip("1.2.3.4")] = new([Group], 1, 20, 1),
        });
        var classifier = new MapClassifier(new Dictionary<string, NormalizationResult>
        {
            ["1.2.3.4"] = Ip("1.2.3.4"),
        });
        var handler = new BulkSearchIocsHandler(classifier, reader);

        var result = await handler.Handle(
            new BulkSearchIocsQuery(["  1.2.3.4  ", "   ", ""]),
            CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.Equal("1.2.3.4", item.Input);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task Handle_FilterEliminatedMatch_IsReportedNotFound()
    {
        var reader = new StubReader(new Dictionary<NormalizationResult, PagedResult<IocGroupResult>>
        {
            [Ip("1.2.3.4")] = PagedResult<IocGroupResult>.Empty(1, 20),
        });
        var handler = new BulkSearchIocsHandler(
            new MapClassifier(new Dictionary<string, NormalizationResult> { ["1.2.3.4"] = Ip("1.2.3.4") }),
            reader);

        var result = await handler.Handle(
            new BulkSearchIocsQuery(["1.2.3.4"], Tag: "botnet", Provider: "1", Malware: " Emotet ", Target: " Amazon "),
            CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.Equal(BulkSearchStatus.NotFound, item.Status);
        Assert.Equal("botnet", reader.Filter!.Tag);
        Assert.Equal("1", reader.Filter.Provider);
        Assert.Equal("Emotet", reader.Filter.Malware);
        Assert.Equal("Amazon", reader.Filter.Target);
    }

    [Fact]
    public async Task Handle_ForwardsResolvedPageBoundsToTheReader()
    {
        var reader = new StubReader(new Dictionary<NormalizationResult, PagedResult<IocGroupResult>>
        {
            [Ip("1.2.3.4")] = new([Group], 2, 5, 12),
        });
        var handler = new BulkSearchIocsHandler(
            new MapClassifier(new Dictionary<string, NormalizationResult> { ["1.2.3.4"] = Ip("1.2.3.4") }),
            reader);

        var result = await handler.Handle(
            new BulkSearchIocsQuery(["1.2.3.4"], Page: 2, PageSize: 5),
            CancellationToken.None);

        Assert.Equal(2, reader.Page);
        Assert.Equal(5, reader.PageSize);
        var item = Assert.Single(result.Items);
        Assert.Equal(2, item.Page);
        Assert.Equal(5, item.PageSize);
        Assert.Equal(12, item.TotalCount);
    }

    [Fact]
    public async Task Handle_TooManyValues_ThrowsValidation()
    {
        var handler = new BulkSearchIocsHandler(new MapClassifier(EmptyMap), new StubReader(EmptyResults));
        var queries = Enumerable.Range(0, BulkSearchLimits.MaxQueries + 1)
            .Select(index => $"value-{index}.example")
            .ToArray();

        var exception = await Assert.ThrowsAsync<IocQueryValidationException>(() =>
            handler.Handle(new BulkSearchIocsQuery(queries), CancellationToken.None));

        Assert.Equal(IocErrorCodes.InvalidInput, exception.Code);
        Assert.Contains("at most", exception.Message);
    }

    [Fact]
    public async Task Handle_OversizedValue_ThrowsValidation()
    {
        var handler = new BulkSearchIocsHandler(new MapClassifier(EmptyMap), new StubReader(EmptyResults));

        await Assert.ThrowsAsync<IocQueryValidationException>(() =>
            handler.Handle(
                new BulkSearchIocsQuery(["a".PadRight(BulkSearchLimits.MaxQueryLength + 1, 'a')]),
                CancellationToken.None));
    }

    [Fact]
    public async Task Handle_EmptyOrMissingQueries_ThrowsValidation()
    {
        var handler = new BulkSearchIocsHandler(new MapClassifier(EmptyMap), new StubReader(EmptyResults));

        await Assert.ThrowsAsync<IocQueryValidationException>(() =>
            handler.Handle(new BulkSearchIocsQuery(null), CancellationToken.None));
        await Assert.ThrowsAsync<IocQueryValidationException>(() =>
            handler.Handle(new BulkSearchIocsQuery([" ", ""]), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_InvalidPageBounds_ThrowsValidation()
    {
        var handler = new BulkSearchIocsHandler(new MapClassifier(EmptyMap), new StubReader(EmptyResults));

        await Assert.ThrowsAsync<IocQueryValidationException>(() =>
            handler.Handle(new BulkSearchIocsQuery(["1.2.3.4"], Page: 0), CancellationToken.None));
        await Assert.ThrowsAsync<IocQueryValidationException>(() =>
            handler.Handle(new BulkSearchIocsQuery(["1.2.3.4"], PageSize: 101), CancellationToken.None));
    }

    private sealed class MapClassifier(IReadOnlyDictionary<string, NormalizationResult> map) : IIocClassifier
    {
        public NormalizationOutput Classify(string query) =>
            map.TryGetValue(query.Trim(), out var identity)
                ? NormalizationOutput.Success(identity)
                : NormalizationOutput.Failure(NormalizationErrorCode.InvalidInput, ErrorMessages.InvalidInput);
    }

    private sealed class StubReader(
        IReadOnlyDictionary<NormalizationResult, PagedResult<IocGroupResult>> results) : IIocQueryReader
    {
        public IReadOnlyList<NormalizationResult> Identities { get; private set; } = [];
        public IocSourceFilter? Filter { get; private set; }
        public int Page { get; private set; }
        public int PageSize { get; private set; }

        public Task<PagedResult<IocGroupResult>> SearchGroupsAsync(
            NormalizationResult identity,
            IocSourceFilter filter,
            int page,
            int pageSize,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyDictionary<NormalizationResult, PagedResult<IocGroupResult>>> SearchBatchAsync(
            IReadOnlyList<NormalizationResult> identities,
            IocSourceFilter filter,
            int page,
            int pageSize,
            CancellationToken cancellationToken)
        {
            Identities = identities;
            Filter = filter;
            Page = page;
            PageSize = pageSize;
            return Task.FromResult(results);
        }

        public Task<IReadOnlyList<IocSuggestionItem>> GetIocSuggestionsAsync(
            string prefix,
            int limit,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IocGroupResult?> GetDetailGroupAsync(int id, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<PagedResult<IocGroupResult>> BrowseAsync(
            IocBrowseCriteria criteria,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<PagedResult<IocGroupResult>?> GetRelatedAsync(
            int id,
            IocRelationKind relation,
            int page,
            int pageSize,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
