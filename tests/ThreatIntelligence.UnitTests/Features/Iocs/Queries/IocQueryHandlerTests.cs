using ThreatIntelligence.Application.Abstractions.Persistence;
using ThreatIntelligence.Application.Features.Iocs.Queries;
using ThreatIntelligence.Application.Features.Iocs.Queries.BrowseIocs;
using ThreatIntelligence.Application.Features.Iocs.Queries.GetIocDetail;
using ThreatIntelligence.Application.Features.Iocs.Queries.GetIocRelations;
using ThreatIntelligence.Application.Normalization;
using ThreatIntelligence.Domain.Enums;

namespace ThreatIntelligence.UnitTests.Features.Iocs.Queries;

public sealed class IocQueryHandlerTests
{
    [Fact]
    public async Task Detail_passes_the_id_and_maps_missing_to_not_found()
    {
        var reader = new StubReader { Detail = null };
        var handler = new GetIocDetailHandler(reader);

        await Assert.ThrowsAsync<IocNotFoundException>(() =>
            handler.Handle(new GetIocDetailQuery(42), CancellationToken.None));
        Assert.Equal(42, reader.DetailId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task Detail_rejects_non_positive_ids(int id)
    {
        var handler = new GetIocDetailHandler(new StubReader());
        var exception = await Assert.ThrowsAsync<IocQueryValidationException>(() =>
            handler.Handle(new GetIocDetailQuery(id), CancellationToken.None));
        Assert.Equal(IocErrorCodes.InvalidInput, exception.Code);
    }

    [Fact]
    public async Task Browse_applies_defaults_and_parses_filters()
    {
        var reader = new StubReader();
        var handler = new BrowseIocsHandler(reader);

        await handler.Handle(
            new BrowseIocsQuery(Type: "URL", OrderBy: "iocValue", Dir: "asc", Page: 2, PageSize: 50),
            CancellationToken.None);

        var criteria = reader.BrowseCriteria!;
        Assert.Equal(IocType.Url, criteria.Type);
        Assert.Equal(IocBrowseOrderBy.IocValue, criteria.OrderBy);
        Assert.False(criteria.Descending);
        Assert.Equal(2, criteria.Page);
        Assert.Equal(50, criteria.PageSize);
    }

    [Fact]
    public async Task Browse_defaults_page_size_and_ordering()
    {
        var reader = new StubReader();
        await new BrowseIocsHandler(reader).Handle(new BrowseIocsQuery(), CancellationToken.None);

        var criteria = reader.BrowseCriteria!;
        Assert.Equal(1, criteria.Page);
        Assert.Equal(20, criteria.PageSize);
        Assert.Equal(IocBrowseOrderBy.LastSeenUtc, criteria.OrderBy);
        Assert.True(criteria.Descending);
    }

    [Theory]
    [InlineData("email", null, null, null, null)]
    [InlineData(null, "providerRecordId", null, null, null)]
    [InlineData(null, null, "sideways", null, null)]
    [InlineData(null, null, null, 0, null)]
    [InlineData(null, null, null, null, 101)]
    public async Task Browse_rejects_invalid_filter_values(
        string? type,
        string? orderBy,
        string? dir,
        int? page,
        int? pageSize)
    {
        var handler = new BrowseIocsHandler(new StubReader());
        await Assert.ThrowsAsync<IocQueryValidationException>(() =>
            handler.Handle(
                new BrowseIocsQuery(Type: type, OrderBy: orderBy, Dir: dir, Page: page, PageSize: pageSize),
                CancellationToken.None));
    }

    [Fact]
    public async Task Browse_normalizes_metadata_filters_and_treats_blank_as_absent()
    {
        var reader = new StubReader();
        await new BrowseIocsHandler(reader).Handle(
            new BrowseIocsQuery(Malware: " Emotet ", Target: "   "),
            CancellationToken.None);

        Assert.Equal("Emotet", reader.BrowseCriteria!.Malware);
        Assert.Null(reader.BrowseCriteria.Target);
    }

    [Fact]
    public async Task Browse_rejects_over_limit_metadata_filters()
    {
        var handler = new BrowseIocsHandler(new StubReader());

        var malware = await Assert.ThrowsAsync<IocQueryValidationException>(() =>
            handler.Handle(new BrowseIocsQuery(Malware: new string('a', 201)), CancellationToken.None));
        Assert.Equal(IocErrorCodes.InvalidInput, malware.Code);

        var target = await Assert.ThrowsAsync<IocQueryValidationException>(() =>
            handler.Handle(new BrowseIocsQuery(Target: new string('a', 501)), CancellationToken.None));
        Assert.Equal(IocErrorCodes.InvalidInput, target.Code);
    }

    [Theory]
    [InlineData("urlHost", IocRelationKind.UrlHost)]
    [InlineData("PARENTDOMAIN", IocRelationKind.ParentDomain)]
    [InlineData("malware", IocRelationKind.Malware)]
    [InlineData("TARGET", IocRelationKind.Target)]
    public async Task Relations_accepts_the_documented_relation_values(
        string relation,
        IocRelationKind expected)
    {
        var reader = new StubReader();
        await new GetIocRelationsHandler(reader).Handle(
            new GetIocRelationsQuery(7, relation, 1, 20),
            CancellationToken.None);

        Assert.Equal(expected, reader.Relation);
        Assert.Equal(7, reader.RelationId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("siblings")]
    public async Task Relations_rejects_unknown_relation_values(string? relation)
    {
        var handler = new GetIocRelationsHandler(new StubReader());
        await Assert.ThrowsAsync<IocQueryValidationException>(() =>
            handler.Handle(new GetIocRelationsQuery(7, relation), CancellationToken.None));
    }

    [Fact]
    public async Task Relations_rejects_oversized_pages_and_missing_ids()
    {
        var handler = new GetIocRelationsHandler(new StubReader());
        await Assert.ThrowsAsync<IocQueryValidationException>(() =>
            handler.Handle(new GetIocRelationsQuery(7, "urlHost", 1, 101), CancellationToken.None));

        var missing = new StubReader { Related = null };
        await Assert.ThrowsAsync<IocNotFoundException>(() =>
            new GetIocRelationsHandler(missing).Handle(
                new GetIocRelationsQuery(7, "urlHost"), CancellationToken.None));
    }

    private sealed class StubReader : IIocQueryReader
    {
        public IocGroupResult? Detail { get; init; }
        public PagedResult<IocGroupResult>? Related { get; init; } = PagedResult<IocGroupResult>.Empty(1, 20);
        public int DetailId { get; private set; }
        public IocBrowseCriteria? BrowseCriteria { get; private set; }
        public int RelationId { get; private set; }
        public IocRelationKind Relation { get; private set; }

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
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<IocSuggestionItem>> GetIocSuggestionsAsync(
            string prefix,
            int limit,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IocGroupResult?> GetDetailGroupAsync(int id, CancellationToken cancellationToken)
        {
            DetailId = id;
            return Task.FromResult(Detail);
        }

        public Task<PagedResult<IocGroupResult>> BrowseAsync(
            IocBrowseCriteria criteria,
            CancellationToken cancellationToken)
        {
            BrowseCriteria = criteria;
            return Task.FromResult(PagedResult<IocGroupResult>.Empty(criteria.Page, criteria.PageSize));
        }

        public Task<PagedResult<IocGroupResult>?> GetRelatedAsync(
            int id,
            IocRelationKind relation,
            int page,
            int pageSize,
            CancellationToken cancellationToken)
        {
            RelationId = id;
            Relation = relation;
            return Task.FromResult(Related);
        }
    }
}
