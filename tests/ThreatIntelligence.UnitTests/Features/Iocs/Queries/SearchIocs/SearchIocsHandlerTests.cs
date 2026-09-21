using ThreatIntelligence.Application.Abstractions.Persistence;
using ThreatIntelligence.Application.Features.Iocs.Queries;
using ThreatIntelligence.Application.Features.Iocs.Queries.SearchIocs;
using ThreatIntelligence.Application.Normalization;
using ThreatIntelligence.Domain.Enums;

namespace ThreatIntelligence.UnitTests.Features.Iocs.Queries.SearchIocs;

public sealed class SearchIocsHandlerTests
{
    private static readonly IocGroupResult Group = new(
        1,
        "example.com",
        "domain",
        null,
        "example.com",
        DateTime.UnixEpoch,
        null,
        []);

    [Fact]
    public async Task Handle_ClassifiesThenDelegatesTheCanonicalIdentityAndFilters()
    {
        var reader = new StubReader(new PagedResult<IocGroupResult>([Group], 1, 20, 1));
        var handler = new SearchIocsHandler(
            new StubClassifier(NormalizationOutput.Success(
                new NormalizationResult(IocType.Domain, "example.com", null, null, "example.com"))),
            reader);

        var result = await handler.Handle(
            new SearchIocsQuery("  EXAMPLE.com  ", " BotNet ", "2"),
            CancellationToken.None);

        Assert.Same(Group, Assert.Single(result.Items));
        Assert.Equal(IocType.Domain, reader.Identity!.Type);
        Assert.Equal("example.com", reader.Identity.IocValue);
        Assert.Equal(" BotNet ", reader.Filter!.Tag);
        Assert.Equal("2", reader.Filter.Provider);
    }

    [Fact]
    public async Task Handle_UnclassifiableInput_ThrowsValidation()
    {
        var handler = new SearchIocsHandler(
            new StubClassifier(NormalizationOutput.Failure(
                NormalizationErrorCode.InvalidInput,
                ErrorMessages.InvalidInput)),
            new StubReader(new PagedResult<IocGroupResult>([Group], 1, 20, 1)));

        var exception = await Assert.ThrowsAsync<IocQueryValidationException>(() =>
            handler.Handle(new SearchIocsQuery("not an ioc at all!"), CancellationToken.None));

        Assert.Equal(IocErrorCodes.InvalidInput, exception.Code);
        Assert.Equal(ErrorMessages.InvalidInput, exception.Message);
    }

    [Fact]
    public async Task Handle_ValidButAbsent_ThrowsNotFound()
    {
        var handler = new SearchIocsHandler(
            new StubClassifier(NormalizationOutput.Success(
                new NormalizationResult(IocType.Ipv4, "1.2.3.4", null, null, null))),
            new StubReader(PagedResult<IocGroupResult>.Empty(1, 20)));

        var exception = await Assert.ThrowsAsync<IocNotFoundException>(() =>
            handler.Handle(new SearchIocsQuery("1.2.3.4"), CancellationToken.None));

        Assert.Equal(IocErrorCodes.NotFound, exception.Code);
    }

    [Fact]
    public async Task Handle_PassesMetadataFiltersAndNormalizesBlank()
    {
        var reader = new StubReader(new PagedResult<IocGroupResult>([Group], 1, 20, 1));
        var handler = new SearchIocsHandler(
            new StubClassifier(NormalizationOutput.Success(
                new NormalizationResult(IocType.Domain, "example.com", null, null, "example.com"))),
            reader);

        await handler.Handle(
            new SearchIocsQuery("example.com", Malware: " Emotet ", Target: "   "),
            CancellationToken.None);

        Assert.Equal("Emotet", reader.Filter!.Malware);
        Assert.Null(reader.Filter.Target);
    }

    [Theory]
    [InlineData(201, 0)]
    [InlineData(0, 501)]
    public async Task Handle_RejectsOverLimitMetadataFilters(int malwareLength, int targetLength)
    {
        var handler = new SearchIocsHandler(
            new StubClassifier(NormalizationOutput.Success(
                new NormalizationResult(IocType.Domain, "example.com", null, null, "example.com"))),
            new StubReader(new PagedResult<IocGroupResult>([Group], 1, 20, 1)));

        var exception = await Assert.ThrowsAsync<IocQueryValidationException>(() =>
            handler.Handle(
                new SearchIocsQuery(
                    "example.com",
                    Malware: malwareLength > 0 ? new string('a', malwareLength) : null,
                    Target: targetLength > 0 ? new string('a', targetLength) : null),
                CancellationToken.None));

        Assert.Equal(IocErrorCodes.InvalidInput, exception.Code);
    }

    [Fact]
    public async Task Handle_AcceptsBoundaryMetadataFilters()
    {
        var reader = new StubReader(new PagedResult<IocGroupResult>([Group], 1, 20, 1));
        var handler = new SearchIocsHandler(
            new StubClassifier(NormalizationOutput.Success(
                new NormalizationResult(IocType.Domain, "example.com", null, null, "example.com"))),
            reader);

        await handler.Handle(
            new SearchIocsQuery(
                "example.com",
                Malware: new string('a', 200),
                Target: new string('b', 500)),
            CancellationToken.None);

        Assert.Equal(200, reader.Filter!.Malware!.Length);
        Assert.Equal(500, reader.Filter.Target!.Length);
    }

    private sealed class StubClassifier(NormalizationOutput output) : IIocClassifier
    {
        public NormalizationOutput Classify(string query) => output;
    }

    private sealed class StubReader(PagedResult<IocGroupResult> result) : IIocQueryReader
    {
        public NormalizationResult? Identity { get; private set; }
        public IocSourceFilter? Filter { get; private set; }

        public Task<PagedResult<IocGroupResult>> SearchGroupsAsync(
            NormalizationResult identity,
            IocSourceFilter filter,
            int page,
            int pageSize,
            CancellationToken cancellationToken)
        {
            Identity = identity;
            Filter = filter;
            return Task.FromResult(result);
        }

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
