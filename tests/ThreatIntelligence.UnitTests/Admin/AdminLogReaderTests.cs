using ThreatIntelligence.Infrastructure.Admin;
using ThreatIntelligence.Infrastructure.Configuration;

namespace ThreatIntelligence.UnitTests.Admin;

public sealed class AdminLogReaderTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"ti-logs-{Guid.NewGuid():N}");

    public AdminLogReaderTests() => Directory.CreateDirectory(_directory);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public async Task Reads_newest_first_across_rotated_files_and_skips_malformed_lines()
    {
        await File.WriteAllLinesAsync(Path.Combine(_directory, "api-20260909.json"),
        [
            Line("2026-09-09T10:00:00Z", "Information", "oldest"),
            "{\"@t\":\"not-json\""
        ]);
        await File.WriteAllLinesAsync(Path.Combine(_directory, "api-20260910.json"),
        [
            Line("2026-09-10T10:00:00Z", "Information", "middle"),
            Line("2026-09-10T11:00:00Z", "Warning", "newest")
        ]);

        var entries = await Reader().ReadAsync(10, null, CancellationToken.None);

        Assert.Equal(["newest", "middle", "oldest"], entries.Select(entry => entry.Message));
        Assert.Equal(3, entries.Count);
    }

    [Fact]
    public async Task Filters_by_level_and_caps_the_count()
    {
        await File.WriteAllLinesAsync(Path.Combine(_directory, "api-20260910.json"),
        [
            Line("2026-09-10T10:00:00Z", "Information", "info"),
            Line("2026-09-10T11:00:00Z", "Warning", "warn"),
            Line("2026-09-10T12:00:00Z", "Error", "error")
        ]);

        var warnings = await Reader().ReadAsync(10, "warning", CancellationToken.None);
        Assert.Equal(["warn"], warnings.Select(entry => entry.Message));

        var capped = await Reader().ReadAsync(1, null, CancellationToken.None);
        Assert.Equal(["error"], capped.Select(entry => entry.Message));
    }

    [Fact]
    public async Task Redacts_secrets_in_messages_and_exceptions()
    {
        await File.WriteAllLinesAsync(Path.Combine(_directory, "api-20260910.json"),
        [
            "{\"@t\":\"2026-09-10T10:00:00Z\",\"@l\":\"Error\",\"@m\":\"failed for apiKey=leaked-key\"," +
            "\"@x\":\"https://user:leaked-pass@host/feed\"}"
        ]);

        var entries = await Reader().ReadAsync(10, null, CancellationToken.None);

        var entry = Assert.Single(entries);
        Assert.DoesNotContain("leaked-key", entry.Message, StringComparison.Ordinal);
        Assert.NotNull(entry.Exception);
        Assert.DoesNotContain("leaked-pass", entry.Exception, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Missing_directory_returns_empty()
    {
        var reader = new AdminLogReader(new AdminLogOptions(Path.Combine(_directory, "absent"), "api-"));

        Assert.Empty(await reader.ReadAsync(10, null, CancellationToken.None));
    }

    private AdminLogReader Reader() => new(new AdminLogOptions(_directory, "api-"));

    private static string Line(string timestamp, string level, string message) =>
        $"{{\"@t\":\"{timestamp}\",\"@l\":\"{level}\",\"@m\":\"{message}\"}}";
}
