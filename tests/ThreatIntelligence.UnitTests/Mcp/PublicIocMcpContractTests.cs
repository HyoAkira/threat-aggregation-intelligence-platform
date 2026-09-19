using System.Text.Json;
using ThreatIntelligence.API.Mcp;

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
}