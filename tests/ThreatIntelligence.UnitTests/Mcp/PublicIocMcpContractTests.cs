using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Server;
using ThreatIntelligence.API.Mcp;
using ThreatIntelligence.Application.Abstractions.Persistence;
using ThreatIntelligence.Application.Features.Iocs.Queries;
using ThreatIntelligence.Application.Features.Iocs.Queries.BrowseIocs;
using ThreatIntelligence.Application.Features.Iocs.Queries.GetIocRelations;
using ThreatIntelligence.Application.Features.Iocs.Queries.SearchIocs;
using ThreatIntelligence.Application.Normalization;

namespace ThreatIntelligence.UnitTests.Mcp;

public sealed class PublicIocMcpContractTests
{
    [Fact]
    public void Paged_response_discloses_when_more_results_are_omitted()
    {
        var response = new PagedIocGroups(
            [],
            page: 2,
            pageSize: 20,
            totalCount: 55,
            isTruncated: true,
            omittedCount: 15);

        var json = JsonSerializer.Serialize(response);

        Assert.Contains("\"isTruncated\":true", json);
        Assert.Contains("\"omittedCount\":15", json);
        Assert.Equal(55, response.totalCount);
    }

    [Fact]
    public void Paged_response_marks_the_last_page_complete()
    {
        var response = new PagedIocGroups(
            [],
            page: 3,
            pageSize: 20,
            totalCount: 55,
            isTruncated: false,
            omittedCount: 0);

        Assert.False(response.isTruncated);
        Assert.Equal(0, response.omittedCount);
    }

    [Fact]
    public async Task Search_tool_passes_trimmed_metadata_filters_through()
    {
        var sender = new CapturingSender();
        var server = CreateServer(sender);

        await server.SearchExactAsync("1.2.3.4", tag: null, provider: null, malware: " Emotet ", target: "   ");

        var query = Assert.IsType<SearchIocsQuery>(sender.LastRequest);
        Assert.Equal("Emotet", query.Malware);
        Assert.Null(query.Target);
    }

    [Fact]
    public async Task Browse_tool_passes_metadata_filters_through()
    {
        var sender = new CapturingSender();
        var server = CreateServer(sender);

        await server.BrowseAsync(malware: "Emotet", target: " Amazon ");

        var query = Assert.IsType<BrowseIocsQuery>(sender.LastRequest);
        Assert.Equal("Emotet", query.Malware);
        Assert.Equal("Amazon", query.Target);
    }

    [Fact]
    public async Task Search_tool_accepts_boundary_lengths()
    {
        var sender = new CapturingSender();
        var server = CreateServer(sender);

        await server.SearchExactAsync("1.2.3.4", malware: new string('a', 200), target: new string('b', 500));

        var query = Assert.IsType<SearchIocsQuery>(sender.LastRequest);
        Assert.Equal(200, query.Malware!.Length);
        Assert.Equal(500, query.Target!.Length);
    }

    [Theory]
    [InlineData(201, 0)]
    [InlineData(0, 501)]
    public async Task Search_tool_rejects_oversized_metadata_filters(int malwareLength, int targetLength)
    {
        var server = CreateServer(new CapturingSender());

        var exception = await Assert.ThrowsAsync<McpToolException>(() =>
            server.SearchExactAsync(
                "1.2.3.4",
                malware: malwareLength > 0 ? new string('a', malwareLength) : null,
                target: targetLength > 0 ? new string('a', targetLength) : null));

        Assert.Equal("invalid_input", exception.Code);
    }

    [Fact]
    public async Task Browse_tool_rejects_oversized_metadata_filters()
    {
        var server = CreateServer(new CapturingSender());

        var exception = await Assert.ThrowsAsync<McpToolException>(() =>
            server.BrowseAsync(target: new string('a', 501)));

        Assert.Equal("invalid_input", exception.Code);
    }

    [Theory]
    [InlineData("malware", IocRelationKind.Malware)]
    [InlineData("TARGET", IocRelationKind.Target)]
    public async Task Relations_tool_accepts_metadata_kinds(string relation, IocRelationKind expected)
    {
        var sender = new CapturingSender();
        var server = CreateServer(sender);

        await server.GetRelationsAsync(7, relation);

        var query = Assert.IsType<GetIocRelationsQuery>(sender.LastRequest);
        Assert.Equal(expected.ToString(), query.Relation);
    }

    [Fact]
    public async Task Relations_tool_returns_the_delegated_page()
    {
        var page = new PagedResult<IocGroupResult>(
            [
                new IocGroupResult(1, "https://a.example/a", "url", "a.example", "a.example", DateTime.UnixEpoch, null, []),
                new IocGroupResult(2, "https://b.example/b", "url", "b.example", "b.example", DateTime.UnixEpoch, null, []),
            ],
            1,
            20,
            2);
        var sender = new CapturingSender { Responder = _ => page };
        var server = CreateServer(sender);

        var result = await server.GetRelationsAsync(7, "malware");

        Assert.Equal(2, result.totalCount);
        Assert.Equal(2, result.items.Count);
    }

    [Fact]
    public async Task Relations_tool_rejects_unsupported_kind()
    {
        var server = CreateServer(new CapturingSender());

        var exception = await Assert.ThrowsAsync<McpToolException>(() =>
            server.GetRelationsAsync(7, "siblings"));

        Assert.Equal("invalid_input", exception.Code);
    }

    [Fact]
    public async Task Relations_tool_maps_unknown_id_to_not_found()
    {
        var sender = new CapturingSender { Responder = _ => null };
        var server = CreateServer(sender);

        var exception = await Assert.ThrowsAsync<McpToolException>(() =>
            server.GetRelationsAsync(7, "target"));

        Assert.Equal("not_found", exception.Code);
    }

    [Fact]
    public void Tool_schema_advertises_metadata_inputs()
    {
        AssertSchemaContains(nameof(PublicIocMcpServer.SearchExactAsync), "malware", "target");
        AssertSchemaContains(nameof(PublicIocMcpServer.BrowseAsync), "malware", "target");
    }

    private static void AssertSchemaContains(string methodName, params string[] expected)
    {
        var method = typeof(PublicIocMcpServer).GetMethod(methodName)!;
        var tool = McpServerTool.Create(method, CreateServer(new CapturingSender()), new McpServerToolCreateOptions());
        var schema = tool.ProtocolTool.InputSchema.GetRawText();

        foreach (var name in expected)
        {
            Assert.Contains($"\"{name}\"", schema);
        }
    }

    private static PublicIocMcpServer CreateServer(ISender sender) =>
        new(sender, new StubReader(), NullLogger<PublicIocMcpServer>.Instance);

    private sealed class CapturingSender : ISender
    {
        public object? LastRequest { get; private set; }

        public Func<object, object?> Responder { get; set; } =
            _ => PagedResult<IocGroupResult>.Empty(1, 20);

        public Task<TResponse> Send<TResponse>(
            IRequest<TResponse> request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult((TResponse)Responder(request)!);
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest =>
            throw new NotSupportedException();

        public Task<object?> Send(object request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
            IStreamRequest<TResponse> request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public IAsyncEnumerable<object?> CreateStream(
            object request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class StubReader : IIocQueryReader
    {
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
