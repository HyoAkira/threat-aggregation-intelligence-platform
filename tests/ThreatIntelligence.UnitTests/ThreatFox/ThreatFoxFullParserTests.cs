using System.IO.Compression;
using System.Text;
using ThreatIntelligence.Application.ProviderRecords;
using ThreatIntelligence.Infrastructure.ThreatFox;

namespace ThreatIntelligence.UnitTests.ThreatFox;

public sealed class ThreatFoxFullParserTests
{
    private readonly ThreatFoxFullParser _parser = new(new ThreatFoxRecentParser(new ThreatFoxRecordMapper()));

    private static MemoryStream ZipStream(params (string Name, string Content)[] entries)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                var entry = archive.CreateEntry(name);
                using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
                writer.Write(content);
            }
        }

        stream.Position = 0;
        return stream;
    }

    private static async Task<List<ProviderRecordInput>> ParseAsync(ThreatFoxFullParser parser, Stream zipStream)
    {
        var records = new List<ProviderRecordInput>();
        await foreach (var record in parser.ParseAsync(zipStream, CancellationToken.None))
        {
            records.Add(record);
        }

        return records;
    }

    [Fact]
    public async Task Expected_entry_is_decompressed_and_records_yielded()
    {
        var json = """[{"id":"1","ioc":"c2-evil.example","ioc_type":"domain","first_seen":"2026-01-01 00:00:00 UTC"}]""";
        await using var zip = ZipStream(("full.json", json));

        var records = await ParseAsync(_parser, zip);

        var record = Assert.Single(records);
        Assert.Equal("c2-evil.example", record.RawIoc);
        Assert.Equal(ThreatFoxRecordMapper.ThreatFoxProviderId, record.ProviderId);
    }

    [Fact]
    public async Task Unsupported_ioc_type_records_are_skipped_and_valid_records_yielded()
    {
        var json = """
        [
          {"id":"1","ioc":"a.example","ioc_type":"domain","first_seen":"2026-01-01 00:00:00 UTC"},
          {"id":"2","ioc":"0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef","ioc_type":"sha256_hash","first_seen":"2026-01-01 00:00:00 UTC"},
          {"id":"3","ioc":"b.example","ioc_type":"domain","first_seen":"2026-01-02 00:00:00 UTC"}
        ]
        """;
        await using var zip = ZipStream(("full.json", json));

        var records = await ParseAsync(_parser, zip);

        Assert.Equal(new[] { "a.example", "b.example" }, records.Select(record => record.RawIoc).ToArray());
    }

    [Fact]
    public async Task Missing_expected_entry_is_rejected_with_a_structured_archive_error()
    {
        await using var zip = ZipStream(("other.json", """[{"id":"1","ioc":"x.com","ioc_type":"domain","first_seen":"2026-01-01 00:00:00 UTC"}]"""));

        var ex = await Assert.ThrowsAsync<ThreatFoxArchiveException>(async () =>
        {
            await foreach (var _ in _parser.ParseAsync(zip, CancellationToken.None)) { }
        });

        Assert.Contains(ThreatFoxFullParser.EntryFileName, ex.Message);
    }

    [Fact]
    public async Task Empty_archive_is_rejected_with_a_structured_archive_error()
    {
        await using var zip = ZipStream();

        var ex = await Assert.ThrowsAsync<ThreatFoxArchiveException>(async () =>
        {
            await foreach (var _ in _parser.ParseAsync(zip, CancellationToken.None)) { }
        });

        Assert.Contains("does not contain any entries", ex.Message);
    }

    [Fact]
    public async Task Excessive_entry_count_is_rejected_before_decompression()
    {
        var guardedParser = new ThreatFoxFullParser(
            new ThreatFoxRecentParser(new ThreatFoxRecordMapper()),
            maxEntries: 1,
            maxUncompressedBytes: ThreatFoxFullParser.DefaultMaxUncompressedBytes);

        await using var zip = ZipStream(
            ("full.json", """[{"id":"1","ioc":"a.com","ioc_type":"domain","first_seen":"2026-01-01 00:00:00 UTC"}]"""),
            ("second.json", """[{"id":"2","ioc":"b.com","ioc_type":"domain","first_seen":"2026-01-01 00:00:00 UTC"}]"""));

        var ex = await Assert.ThrowsAsync<ThreatFoxArchiveException>(async () =>
        {
            await foreach (var _ in guardedParser.ParseAsync(zip, CancellationToken.None)) { }
        });

        Assert.Contains("exceeding the guard", ex.Message);
    }

    [Fact]
    public async Task Declared_uncompressed_size_guard_rejects_before_full_decompression()
    {
        var guardedParser = new ThreatFoxFullParser(
            new ThreatFoxRecentParser(new ThreatFoxRecordMapper()),
            maxEntries: ThreatFoxFullParser.DefaultMaxEntries,
            maxUncompressedBytes: 32);

        await using var zip = ZipStream(("full.json", """[{"id":"1","ioc":"a.com","ioc_type":"domain","first_seen":"2026-01-01 00:00:00 UTC"}]"""));

        var ex = await Assert.ThrowsAsync<ThreatFoxArchiveException>(async () =>
        {
            await foreach (var _ in guardedParser.ParseAsync(zip, CancellationToken.None)) { }
        });

        Assert.Contains("uncompressed bytes", ex.Message);
    }

    [Fact]
    public async Task Corrupt_or_truncated_archive_fails_with_a_structured_error()
    {
        await using var zip = ZipStream(("full.json", """[{"id":"1","ioc":"a.com","ioc_type":"domain","first_seen":"2026-01-01 00:00:00 UTC"}]"""));
        var bytes = zip.ToArray();
        var truncated = new MemoryStream(bytes.Take(bytes.Length / 2).ToArray());

        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await foreach (var _ in _parser.ParseAsync(truncated, CancellationToken.None)) { }
        });
    }
}