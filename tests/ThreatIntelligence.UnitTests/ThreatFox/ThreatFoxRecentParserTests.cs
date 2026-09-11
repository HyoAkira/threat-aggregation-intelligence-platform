using System.Text;
using ThreatIntelligence.Application.ProviderRecords;
using ThreatIntelligence.Domain.Enums;
using ThreatIntelligence.Infrastructure.ThreatFox;

namespace ThreatIntelligence.UnitTests.ThreatFox;

public sealed class ThreatFoxRecentParserTests
{
    private readonly ThreatFoxRecentParser _parser = new(new ThreatFoxRecordMapper());

    private static MemoryStream StreamOf(string json) => new(Encoding.UTF8.GetBytes(json));

    private static async Task<List<ProviderRecordInput>> ParseAsync(ThreatFoxRecentParser parser, string json)
    {
        await using var stream = StreamOf(json);
        var records = new List<ProviderRecordInput>();
        await foreach (var record in parser.ParseAsync(stream, CancellationToken.None))
        {
            records.Add(record);
        }

        return records;
    }

    [Fact]
    public async Task Domain_record_is_normalized_as_a_domain_ioc()
    {
        var json = """[{"id":"1","ioc":"Api.Example.CO.UK.","ioc_type":"domain","first_seen":"2026-01-01 00:00:00 UTC","confidence_level":50}]""";

        var records = await ParseAsync(_parser, json);

        var record = Assert.Single(records);
        Assert.Equal(1, record.ProviderId);
        Assert.Equal("1", record.ProviderRecordId);
        Assert.Equal("Api.Example.CO.UK.", record.RawIoc);
        Assert.Equal(IocType.Domain, record.DeclaredType);
        Assert.Equal(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), record.FirstSeenAt);
        Assert.Null(record.LastSeenAt);
        Assert.Equal("https://threatfox.abuse.ch/ioc/1", record.ProviderDetailsLink);
    }

    [Fact]
    public async Task Url_record_preserves_path_and_query()
    {
        var json = """[{"id":"2","ioc":"https://evil.example/path?q=1","ioc_type":"url","first_seen":"2026-01-02 00:00:00 UTC","last_seen":"2026-01-03 12:00:00 UTC"}]""";

        var records = await ParseAsync(_parser, json);

        var record = Assert.Single(records);
        Assert.Equal(IocType.Url, record.DeclaredType);
        Assert.Equal("https://evil.example/path?q=1", record.RawIoc);
        Assert.Equal(
            new DateTime(2026, 1, 3, 12, 0, 0, DateTimeKind.Utc),
            record.LastSeenAt);
    }

    [Fact]
    public async Task Ip_port_record_carries_the_raw_endpoint_and_no_fixed_declared_type()
    {
        var json = """[{"id":"3","ioc":"185.132.53.205:1312","ioc_type":"ip:port","first_seen":"2026-01-04 00:00:00 UTC"}]""";

        var records = await ParseAsync(_parser, json);

        var record = Assert.Single(records);
        Assert.Equal("185.132.53.205:1312", record.RawIoc);
        Assert.Null(record.DeclaredType);
    }

    [Fact]
    public async Task Feed_is_parsed_as_records_without_requiring_a_single_line()
    {
        var json = "[\n  { \"id\": \"1\", \"ioc\": \"a.com\", \"ioc_type\": \"domain\", \"first_seen\": \"2026-01-01 00:00:00 UTC\" },\n  { \"id\": \"2\", \"ioc\": \"b.com\", \"ioc_type\": \"domain\", \"first_seen\": \"2026-01-02 00:00:00 UTC\" }\n]";

        var records = await ParseAsync(_parser, json);

        Assert.Equal(2, records.Count);
        Assert.Equal("a.com", records[0].RawIoc);
        Assert.Equal("b.com", records[1].RawIoc);
    }

    [Fact]
    public async Task Malformed_record_raises_a_structured_failure_naming_record_and_cause()
    {
        var json = """[{"id":"1","ioc":"a.com","ioc_type":"domain","first_seen":"2026-01-01 00:00:00 UTC"},{"id":"2","ioc":"b.com","ioc_type":"domain","first_seen":"not-a-date"}]""";

        var ex = await Assert.ThrowsAsync<ThreatFoxRecordParseException>(() =>
            ParseAsync(_parser, json));

        Assert.Equal(2, ex.RecordIndex);
        Assert.Equal("first_seen", ex.Field);
        Assert.False(string.IsNullOrWhiteSpace(ex.Reason));
        Assert.Contains("2", ex.Message);
    }

    [Fact]
    public async Task A_keyed_object_whose_values_are_not_arrays_raises_a_feed_format_failure_carrying_the_payload_prefix()
    {
        var json = """{"query_status":"no_results"}""";

        var ex = await Assert.ThrowsAsync<ThreatFoxFeedFormatException>(() =>
            ParseAsync(_parser, json));

        Assert.Contains("no_results", ex.PayloadPrefix);
        Assert.Contains("no_results", ex.Message);
    }

    [Fact]
    public async Task An_unsupported_root_token_raises_a_feed_format_failure_carrying_the_payload_prefix()
    {
        var json = """<html>not json</html>""";

        var ex = await Assert.ThrowsAsync<ThreatFoxFeedFormatException>(() =>
            ParseAsync(_parser, json));

        Assert.Contains("<html>", ex.PayloadPrefix);
    }

    [Fact]
    public async Task Keyed_object_export_maps_id_from_the_key_and_export_field_names()
    {
        var json = """
        {
          "1911444": [
            {
              "ioc_value": "154.91.62.102:8074",
              "ioc_type": "ip:port",
              "threat_type": "botnet_cc",
              "malware": "win.vshell",
              "malware_alias": null,
              "malware_printable": "VShell",
              "first_seen_utc": "2026-09-10 23:05:07",
              "last_seen_utc": null,
              "confidence_level": 100,
              "is_compromised": true,
              "reference": null,
              "tags": "vshell",
              "anonymous": 1,
              "reporter": "anonymous"
            }
          ]
        }
        """;

        var records = await ParseAsync(_parser, json);

        var record = Assert.Single(records);
        Assert.Equal("1911444", record.ProviderRecordId);
        Assert.Equal("154.91.62.102:8074", record.RawIoc);
        Assert.Null(record.DeclaredType);
        Assert.Equal(new DateTime(2026, 9, 10, 23, 5, 7, DateTimeKind.Utc), record.FirstSeenAt);
        Assert.Null(record.LastSeenAt);
        Assert.Equal("https://threatfox.abuse.ch/ioc/1911444", record.ProviderDetailsLink);
        Assert.Contains("vshell", record.Tags!.Select(tag => tag!).ToArray());
        Assert.Equal(100, Assert.IsType<int>(record.Metadata![ThreatFoxRecordMapper.ConfidenceLevelKey]));
    }

    [Fact]
    public async Task Keyed_object_export_splits_a_comma_separated_tag_string()
    {
        var json = """{"1":[{"ioc_value":"evil.example","ioc_type":"domain","first_seen_utc":"2026-01-01 00:00:00","tags":"c2, malware ,c2"}]}""";

        var records = await ParseAsync(_parser, json);

        var record = Assert.Single(records);
        Assert.Equal(new[] { "c2", "malware" }, record.Tags!.Select(tag => tag!).ToArray());
    }

    [Fact]
    public async Task Multiple_records_under_one_key_are_all_mapped_to_that_id()
    {
        var json = """{"42":[{"ioc_value":"a.example","ioc_type":"domain","first_seen_utc":"2026-01-01 00:00:00"},{"ioc_value":"b.example","ioc_type":"domain","first_seen_utc":"2026-01-02 00:00:00"}]}""";

        var records = await ParseAsync(_parser, json);

        Assert.Equal(2, records.Count);
        Assert.All(records, record => Assert.Equal("42", record.ProviderRecordId));
        Assert.Equal(new[] { "a.example", "b.example" }, records.Select(record => record.RawIoc!).ToArray());
    }

    [Fact]
    public async Task An_empty_body_raises_a_feed_format_failure_naming_it_empty()
    {
        var ex = await Assert.ThrowsAsync<ThreatFoxFeedFormatException>(() =>
            ParseAsync(_parser, string.Empty));

        Assert.Contains("empty", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_blank_required_field_is_reported_with_record_context()
    {
        var json = """[{"id":"1","ioc":"  ","ioc_type":"domain","first_seen":"2026-01-01 00:00:00 UTC"}]""";

        var ex = await Assert.ThrowsAsync<ThreatFoxRecordParseException>(() =>
            ParseAsync(_parser, json));

        Assert.Equal(1, ex.RecordIndex);
        Assert.Equal("ioc", ex.Field);
    }

    [Fact]
    public async Task Unsupported_ioc_type_is_reported_with_record_context()
    {
        var json = """[{"id":"1","ioc":"0123456789abcdef","ioc_type":"md5_hash","first_seen":"2026-01-01 00:00:00 UTC"}]""";

        var ex = await Assert.ThrowsAsync<ThreatFoxRecordParseException>(() =>
            ParseAsync(_parser, json));

        Assert.Equal(1, ex.RecordIndex);
        Assert.Equal("ioc_type", ex.Field);
    }
}