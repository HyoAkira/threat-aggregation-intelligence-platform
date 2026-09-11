using ThreatIntelligence.Application.Abstractions.Persistence;
using ThreatIntelligence.Application.Features.Iocs.Queries;
using ThreatIntelligence.Application.Features.Iocs.Queries.SearchIocs;
using ThreatIntelligence.Application.Normalization;
using ThreatIntelligence.Domain.Enums;

namespace ThreatIntelligence.UnitTests.Features.Iocs.Queries.SearchIocs;

public sealed class SearchIocsHandlerTests
{
    private static readonly IocGroupResult Group = new(
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
        var reader = new StubReader(Group);
        var handler = new SearchIocsHandler(
            new StubClassifier(NormalizationOutput.Success(
                new NormalizationResult(IocType.Domain, "example.com", null, null, "example.com"))),
            reader);

        var result = await handler.Handle(
            new SearchIocsQuery("  EXAMPLE.com  ", " BotNet ", 2),
            CancellationToken.None);

        Assert.Same(Group, result);
        Assert.Equal(IocType.Domain, reader.Identity!.Type);
        Assert.Equal("example.com", reader.Identity.IocValue);
        Assert.Equal(" BotNet ", reader.Filter!.Tag);
        Assert.Equal(2, reader.Filter.ProviderId);
    }

    [Fact]
    public async Task Handle_UnclassifiableInput_ThrowsValidation()
    {
        var handler = new SearchIocsHandler(
            new StubClassifier(NormalizationOutput.Failure(
                NormalizationErrorCode.InvalidInput,
                ErrorMessages.InvalidInput)),
            new StubReader(Group));

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
            new StubReader(null));

        var exception = await Assert.ThrowsAsync<IocNotFoundException>(() =>
            handler.Handle(new SearchIocsQuery("1.2.3.4"), CancellationToken.None));

        Assert.Equal(IocErrorCodes.NotFound, exception.Code);
    }

    private sealed class StubClassifier(NormalizationOutput output) : IIocClassifier
    {
        public NormalizationOutput Classify(string query) => output;
    }

    private sealed class StubReader(IocGroupResult? result) : IIocQueryReader
    {
        public NormalizationResult? Identity { get; private set; }
        public IocSourceFilter? Filter { get; private set; }

        public Task<IocGroupResult?> SearchGroupAsync(
            NormalizationResult identity,
            IocSourceFilter filter,
            CancellationToken cancellationToken)
        {
            Identity = identity;
            Filter = filter;
            return Task.FromResult(result);
        }

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
