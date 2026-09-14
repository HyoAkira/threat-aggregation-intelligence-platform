using System.Text.Json;
using ThreatIntelligence.Infrastructure.Admin;
using ThreatIntelligence.Infrastructure.Configuration;
using Xunit;

namespace ThreatIntelligence.UnitTests.Admin;

public sealed class AdminLogReaderTests : IDisposable
{
    private readonly string _directory;

    public AdminLogReaderTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"admin-log-reader-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
    }

    private AdminLogReader CreateReader() => new(new AdminLogOptions(_directory, "api-"));

    [Fact]
    public async Task Defaults_missing_level_to_information_for_clef_entries()
    {
        await File.WriteAllTextAsync(
            Path.Combine(_directory, "api-20260914.json"),
            """{"@t":"2026-09-14T11:20:43.8367037Z","@mt":"HTTP {RequestMethod} {RequestPath} responded {StatusCode}","RequestMethod":"GET","RequestPath":"/health","StatusCode":200}""");

        var entries = await CreateReader().ReadAsync(count: 10, level: null, CancellationToken.None);

        var entry = Assert.Single(entries);
        Assert.Equal("Information", entry.Level);
        Assert.Equal("HTTP GET /health responded 200", entry.Message);
    }

    [Fact]
    public async Task Renders_holes_with_formats_from_pre_rendered_values()
    {
        await File.WriteAllTextAsync(
            Path.Combine(_directory, "api-20260914.json"),
            """{"@t":"2026-09-14T11:20:43.8367037Z","@mt":"HTTP GET /health responded 200 in {Elapsed:0.0000} ms","Elapsed":12.3456789,"@r":["12.3457"]}""");

        var entries = await CreateReader().ReadAsync(count: 10, level: null, CancellationToken.None);

        Assert.Equal("HTTP GET /health responded 200 in 12.3457 ms", Assert.Single(entries).Message);
    }

    [Fact]
    public async Task Pairs_multiple_pre_rendered_values_with_holes_in_order()
    {
        await File.WriteAllTextAsync(
            Path.Combine(_directory, "api-20260914.json"),
            """{"@t":"2026-09-14T11:20:43.8367037Z","@mt":"Retried after {Delay:0.0}s, backing off for {Backoff:0.0}s","Delay":1.25,"Backoff":2.625,"@r":["1.3","2.6"]}""");

        var entries = await CreateReader().ReadAsync(count: 10, level: null, CancellationToken.None);

        Assert.Equal("Retried after 1.3s, backing off for 2.6s", Assert.Single(entries).Message);
    }

    [Fact]
    public async Task Renders_numeric_holes_when_no_pre_rendered_value_exists()
    {
        await File.WriteAllTextAsync(
            Path.Combine(_directory, "api-20260914.json"),
            """{"@t":"2026-09-14T11:20:43.8367037Z","@mt":"Retried {Attempts} times after {Delay:0.0} seconds","Attempts":3,"Delay":1.25}""");

        var entries = await CreateReader().ReadAsync(count: 10, level: null, CancellationToken.None);

        Assert.Equal("Retried 3 times after 1.3 seconds", Assert.Single(entries).Message);
    }

    [Fact]
    public async Task Keeps_literal_hole_text_for_missing_properties()
    {
        await File.WriteAllTextAsync(
            Path.Combine(_directory, "api-20260914.json"),
            """{"@t":"2026-09-14T11:20:43.8367037Z","@mt":"Failed {Endpoint} on attempt {Attempt}"}""");

        var entries = await CreateReader().ReadAsync(count: 10, level: null, CancellationToken.None);

        Assert.Equal("Failed {Endpoint} on attempt {Attempt}", Assert.Single(entries).Message);
    }

    [Fact]
    public async Task Returns_the_correlation_id_captured_with_the_event()
    {
        await File.WriteAllTextAsync(
            Path.Combine(_directory, "api-20260914.json"),
            """{"@t":"2026-09-14T11:20:43.8367037Z","@l":"Error","@mt":"Fetch failed","CorrelationId":"corr-42","@x":"System.InvalidOperationException: boom"}""");

        var entries = await CreateReader().ReadAsync(count: 10, level: null, CancellationToken.None);

        Assert.Equal("corr-42", Assert.Single(entries).CorrelationId);
    }

    [Fact]
    public async Task Level_filter_matches_default_information_entries()
    {
        await File.WriteAllTextAsync(
            Path.Combine(_directory, "api-20260914.json"),
            """{"@t":"2026-09-14T11:20:43.8367037Z","@mt":"HTTP GET /health responded 200"}""");

        var entries = await CreateReader().ReadAsync(count: 10, level: "Information", CancellationToken.None);

        Assert.NotEmpty(entries);
    }

    [Fact]
    public async Task Reads_newest_first_and_parses_rendered_messages()
    {
        await File.WriteAllTextAsync(
            Path.Combine(_directory, "api-20260914.json"),
            string.Join('\n',
                """{"@t":"2026-09-14T10:00:00.0000000Z","@l":"Warning","@mt":"First message"}""",
                """{"@t":"2026-09-14T11:00:00.0000000Z","@l":"Error","@mt":"Second","@x":"System.InvalidOperationException: boom"}"""));

        var entries = await CreateReader().ReadAsync(count: 10, level: null, CancellationToken.None);

        Assert.Equal(2, entries.Count);
        Assert.Equal("Error", entries[0].Level);
        Assert.Equal("Second", entries[0].Message);
        Assert.Contains("boom", entries[0].Exception);
        Assert.Equal("Warning", entries[1].Level);
        Assert.Equal("First message", entries[1].Message);
    }

    [Fact]
    public async Task Skips_malformed_lines_and_returns_empty_when_directory_is_missing()
    {
        await File.WriteAllTextAsync(
            Path.Combine(_directory, "api-20260914.json"),
            """{"@t":"2026-09-14T11:00:00.0000000Z","@l":"Error","@mt":"Valid"}""" + "\nnot-json\n");

        var parsed = await CreateReader().ReadAsync(count: 10, level: null, CancellationToken.None);
        Assert.Single(parsed);

        var missing = await new AdminLogReader(
            new AdminLogOptions(Path.Combine(_directory, "does-not-exist"), "api-"))
            .ReadAsync(count: 10, level: null, CancellationToken.None);
        Assert.Empty(missing);
    }
}
