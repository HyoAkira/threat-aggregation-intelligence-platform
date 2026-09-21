using System.IO.Compression;
using System.Text;
using ThreatIntelligence.Application.Fetches;
using ThreatIntelligence.Application.ProviderRecords;
using ThreatIntelligence.Infrastructure.PhishTank;

namespace ThreatIntelligence.UnitTests.PhishTank;

public sealed class PhishTankFeedParserTests
{
    private readonly PhishTankFeedParser _parser = new(new PhishTankRecordMapper());

    private static MemoryStream GzipStreamOf(string json)
    {
        var stream = new MemoryStream();
        using (var gzip = new GZipStream(stream, CompressionMode.Compress, leaveOpen: true))
        using (var writer = new StreamWriter(gzip, Encoding.UTF8))
        {
            writer.Write(json);
        }
        stream.Position = 0;
        return stream;
    }

    private static async Task<List<ProviderRecordInput>> ParseAsync(PhishTankFeedParser parser, Stream stream)
    {
        var records = new List<ProviderRecordInput>();
        await foreach (var record in parser.ParseAsync(stream, CancellationToken.None))
        {
            records.Add(record);
        }
        return records;
    }

    [Fact]
    public async Task Valid_gzip_feed_is_parsed_into_provider_records()
    {
        var json = """[{"phish_id":1,"url":"http://evil.example/phish","phish_detail_url":"http://data.phishtank.com/data/1/detail","submission_time":"2026-01-01T12:00:00+00:00","verification_time":"2026-01-01T12:30:00+00:00","verified":"yes","online":"yes","target":"Example Bank","details":[{"ip_address":"1.2.3.4","cidr_block":"1.2.3.0/24","announcing_network":"1234","rir":"arin","detail_time":"2026-01-01T12:30:00+00:00"}]}]""";
        await using var stream = GzipStreamOf(json);

        var records = await ParseAsync(_parser, stream);

        var record = Assert.Single(records);
        Assert.Equal(2, record.ProviderId);
        Assert.Equal("1", record.ProviderRecordId);
        Assert.Equal("http://evil.example/phish", record.RawIoc);
        Assert.Equal(ThreatIntelligence.Domain.Enums.IocType.Url, record.DeclaredType);
        Assert.Equal(new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc), record.FirstSeenAt);
        Assert.Equal(new DateTime(2026, 1, 1, 12, 30, 0, DateTimeKind.Utc), record.LastSeenAt);
        Assert.Equal("http://data.phishtank.com/data/1/detail", record.ProviderDetailsLink);
    }

    [Fact]
    public async Task Multiple_records_are_streamed_without_buffering()
    {
        var json = """
        [
          {"phish_id":1,"url":"http://a.example","submission_time":"2026-01-01T00:00:00+00:00"},
          {"phish_id":2,"url":"http://b.example","submission_time":"2026-01-02T00:00:00+00:00"},
          {"phish_id":3,"url":"http://c.example","submission_time":"2026-01-03T00:00:00+00:00"}
        ]
        """;
        await using var stream = GzipStreamOf(json);

        var records = await ParseAsync(_parser, stream);

        Assert.Equal(3, records.Count);
        Assert.Equal("http://a.example", records[0].RawIoc);
        Assert.Equal("http://b.example", records[1].RawIoc);
        Assert.Equal("http://c.example", records[2].RawIoc);
    }

    [Fact]
    public async Task Corrupt_gzip_fails_with_decompression_exception()
    {
        var corruptBytes = Encoding.UTF8.GetBytes("this is not gzip");
        await using var stream = new MemoryStream(corruptBytes);

        var ex = await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            await foreach (var _ in _parser.ParseAsync(stream, CancellationToken.None)) { }
        });

        Assert.False(FetchErrorClassifier.IsTransient(ex));
    }

    [Fact]
    public async Task Truncated_gzip_fails_with_json_exception()
    {
        var json = """[{"phish_id":1,"url":"http://evil.example","submission_time":"2026-01-01T00:00:00+00:00"}]""";
        await using var fullStream = GzipStreamOf(json);
        var bytes = fullStream.ToArray();
        var truncated = new MemoryStream(bytes.Take(bytes.Length / 2).ToArray());

        var ex = await Assert.ThrowsAsync<System.Text.Json.JsonException>(async () =>
        {
            await foreach (var _ in _parser.ParseAsync(truncated, CancellationToken.None)) { }
        });

        Assert.False(FetchErrorClassifier.IsTransient(ex));
    }

    [Fact]
    public async Task Invalid_json_fails_with_record_parse_exception()
    {
        var stream = new MemoryStream();
        using (var gzip = new GZipStream(stream, CompressionMode.Compress, leaveOpen: true))
        using (var writer = new StreamWriter(gzip, Encoding.UTF8))
        {
            writer.Write("this is not json");
        }
        stream.Position = 0;

        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await foreach (var _ in _parser.ParseAsync(stream, CancellationToken.None)) { }
        });
    }

    [Fact]
    public async Task Record_with_missing_required_field_fails_with_record_context()
    {
        var json = """[{"phish_id":1,"url":"http://evil.example","submission_time":"2026-01-01T00:00:00+00:00"},{"phish_id":2,"submission_time":"2026-01-02T00:00:00+00:00"}]""";
        await using var stream = GzipStreamOf(json);

        var ex = await Assert.ThrowsAsync<PhishTankRecordParseException>(async () =>
        {
            await foreach (var _ in _parser.ParseAsync(stream, CancellationToken.None)) { }
        });

        Assert.Equal(2, ex.RecordIndex);
        Assert.Equal("url", ex.Field);
    }

    [Fact]
    public async Task Record_with_invalid_timestamp_fails_with_record_context()
    {
        var json = """[{"phish_id":1,"url":"http://evil.example","submission_time":"not-a-date"}]""";
        await using var stream = GzipStreamOf(json);

        var ex = await Assert.ThrowsAsync<PhishTankRecordParseException>(async () =>
        {
            await foreach (var _ in _parser.ParseAsync(stream, CancellationToken.None)) { }
        });

        Assert.Equal(1, ex.RecordIndex);
        Assert.Equal("submission_time", ex.Field);
    }

    [Fact]
    public async Task Empty_array_yields_no_records()
    {
        var json = "[]";
        await using var stream = GzipStreamOf(json);

        var records = await ParseAsync(_parser, stream);

        Assert.Empty(records);
    }

    [Fact]
    public async Task Record_with_null_details_yields_null_payload()
    {
        var json = """[{"phish_id":1,"url":"http://evil.example","submission_time":"2026-01-01T00:00:00+00:00","details":null}]""";
        await using var stream = GzipStreamOf(json);

        var records = await ParseAsync(_parser, stream);

        var record = Assert.Single(records);
        Assert.Null(record.Payload);
    }

    [Fact]
    public async Task Record_with_empty_details_array_yields_null_payload()
    {
        var json = """[{"phish_id":1,"url":"http://evil.example","submission_time":"2026-01-01T00:00:00+00:00","details":[]}]""";
        await using var stream = GzipStreamOf(json);

        var records = await ParseAsync(_parser, stream);

        var record = Assert.Single(records);
        Assert.Null(record.Payload);
    }

    [Fact]
    public async Task Cancellation_is_respected()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var json = """[{"phish_id":1,"url":"http://evil.example","submission_time":"2026-01-01T00:00:00+00:00"},{"phish_id":2,"url":"http://evil2.example","submission_time":"2026-01-02T00:00:00+00:00"}]""";
        await using var stream = GzipStreamOf(json);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in _parser.ParseAsync(stream, cts.Token)) { }
        });
    }
}
