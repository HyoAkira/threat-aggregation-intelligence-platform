using System.Text.Json;
using System.Text.Json.Nodes;
using ThreatIntelligence.API.Serialization;
using ThreatIntelligence.Application.Features.Iocs.Queries;

namespace ThreatIntelligence.IntegrationTests;

/// <summary>
/// Locks the shared source-attributed wire contract: lowercase <c>iocType</c> token, per-source
/// provider fields, retained <c>false</c>/<c>0</c>/empty values, the computed <c>isFalsePositive</c>
/// flag, and the global omission of provider-inapplicable nulls.
/// </summary>
public sealed class IocResponseContractTests
{
    private static readonly DateTime FirstSeen = new(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime LastSeen = new(2026, 9, 7, 10, 30, 0, DateTimeKind.Utc);

    [Fact]
    public void Grouped_detail_response_matches_the_golden_contract()
    {
        var expected = JsonNode.Parse(
            """
            {
              "iocValue": "https://evil.example/x?arg=1",
              "iocType": "url",
              "urlHost": "evil.example",
              "parentDomain": "evil.example",
              "firstSeenUtc": "2026-09-01T08:00:00Z",
              "lastSeenUtc": "2026-09-07T10:30:00Z",
              "sources": [
                {
                  "provider": "ThreatFox",
                  "providerRecordId": "12345",
                  "providerDetailsLink": "https://threatfox.abuse.ch/ioc/12345/",
                  "firstSeenUtc": "2026-09-01T08:00:00Z",
                  "lastSeenUtc": "2026-09-07T10:30:00Z",
                  "threatType": "botnetCc",
                  "malware": "Emotet",
                  "threatFoxConfidenceLevel": 90,
                  "tags": ["botnet"],
                  "isFalsePositive": false
                },
                {
                  "provider": "PhishTank",
                  "providerRecordId": "67890",
                  "providerDetailsLink": "https://phishtank.org/phish_detail.php?phish_id=67890",
                  "firstSeenUtc": "2026-09-01T08:00:00Z",
                  "lastSeenUtc": "2026-09-07T10:30:00Z",
                  "isVerified": true,
                  "target": "Example Bank",
                  "phishTankDetails": [],
                  "phishTankOnline": true,
                  "tags": [],
                  "isFalsePositive": false
                }
              ]
            }
            """)!;

        var actual = JsonSerializer.SerializeToNode(BuildGroup(), ApiJson.Create());

        Assert.True(
            JsonNode.DeepEquals(expected, actual),
            $"Wire contract drifted. Actual: {actual?.ToJsonString()}");
    }

    [Fact]
    public void Paginated_envelope_uses_the_shared_shape()
    {
        var page = new PagedResult<IocGroupResult>([BuildGroup()], 1, 20, 1);
        var node = JsonSerializer.SerializeToNode(page, ApiJson.Create())!.AsObject();

        Assert.Equal(1, (int)node["page"]!);
        Assert.Equal(20, (int)node["pageSize"]!);
        Assert.Equal(1, (int)node["totalCount"]!);
        Assert.NotNull(node["items"]);
    }

    private static IocGroupResult BuildGroup()
    {
        var threatFox = new IocSourceResult(
            "ThreatFox", "12345", "https://threatfox.abuse.ch/ioc/12345/", null, FirstSeen, LastSeen,
            "botnetCc", "Emotet", null, null, null, null, null, null, null, null, null, 90,
            ["botnet"], false);

        var phishTank = new IocSourceResult(
            "PhishTank", "67890", "https://phishtank.org/phish_detail.php?phish_id=67890", null,
            FirstSeen, LastSeen, null, null, null, null, null, null, null, true, "Example Bank",
            JsonDocument.Parse("[]").RootElement.Clone(), true, null, [], false);

        return new IocGroupResult(
            "https://evil.example/x?arg=1",
            "url",
            "evil.example",
            "evil.example",
            FirstSeen,
            LastSeen,
            [threatFox, phishTank]);
    }
}
