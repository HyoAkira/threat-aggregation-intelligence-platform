using ThreatIntelligence.Application.Abstractions.Persistence;
using ThreatIntelligence.Application.Features.Iocs.Queries.SearchIocs;
using ThreatIntelligence.Domain.Enums;

namespace ThreatIntelligence.UnitTests.Features.Iocs.Queries.SearchIocs;

public sealed class SearchIocsHandlerTests
{
    [Fact]
    public async Task Handle_DelegatesTheExactQueryAndTagToTheReader()
    {
        var expected = new IocSearchResult(
            "example.com",
            IocType.Domain,
            null,
            "example.com",
            DateTime.UnixEpoch,
            null,
            []);
        var reader = new CapturingIocSearchReader(expected);
        var handler = new SearchIocsHandler(reader);

        var result = await handler.Handle(
            new SearchIocsQuery("example.com", "malware"),
            CancellationToken.None);

        Assert.Same(expected, result);
        Assert.Equal("example.com", reader.Query);
        Assert.Equal("malware", reader.Tag);
    }

    private sealed class CapturingIocSearchReader(IocSearchResult result) : IIocSearchReader
    {
        public string? Query { get; private set; }
        public string? Tag { get; private set; }

        public Task<IocSearchResult?> SearchExactAsync(
            string query,
            string? tag,
            CancellationToken cancellationToken)
        {
            Query = query;
            Tag = tag;
            return Task.FromResult<IocSearchResult?>(result);
        }
    }
}
