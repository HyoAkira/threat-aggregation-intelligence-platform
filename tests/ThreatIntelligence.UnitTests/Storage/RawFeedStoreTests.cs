using System.Text;
using ThreatIntelligence.Application.Fetches;
using ThreatIntelligence.Infrastructure.Configuration;
using ThreatIntelligence.Infrastructure.Storage;

namespace ThreatIntelligence.UnitTests.Storage;

public sealed class RawFeedStoreTests : IDisposable
{
    private static readonly DateTimeOffset FixedNow = new(2026, 9, 11, 14, 49, 0, TimeSpan.Zero);

    private readonly string _root = Path.Combine(Path.GetTempPath(), "tieto-raw-" + Guid.NewGuid().ToString("N"));

    private RawFeedStore CreateStore(string? basePath = null, DateTimeOffset? now = null)
        => new(new RawStorageOptions(basePath ?? _root), new FixedTimeProvider(now ?? FixedNow));

    public void Dispose()
    {
        if (File.Exists(_root))
        {
            File.Delete(_root);
        }
        else if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task Recent_save_writes_timestamped_file_under_the_recents_folder()
    {
        var store = CreateStore();

        var saved = await store.SaveAsync(
            "threatfox",
            RawFeedCategory.Recent,
            "recent.json",
            StreamOf("streamed payload"),
            CancellationToken.None);

        var expectedPath = Path.Combine(_root, "threatfox", "recents", "recent_20260911T144900Z.json");
        Assert.Equal(expectedPath, saved.Path);
        Assert.Equal("recent_20260911T144900Z.json", saved.FileName);
        Assert.Equal("threatfox", saved.ProviderSlug);
        Assert.Equal(RawFeedCategory.Recent, saved.Category);
        Assert.True(File.Exists(expectedPath));
        Assert.Equal("streamed payload", await File.ReadAllTextAsync(expectedPath));
        Assert.Equal(Encoding.UTF8.GetByteCount("streamed payload"), saved.Length);
    }

    [Fact]
    public async Task Full_save_writes_under_the_fulls_folder()
    {
        var store = CreateStore();

        var saved = await store.SaveAsync(
            "threatfox",
            RawFeedCategory.Full,
            "full.json.zip",
            StreamOf("PK-zip"),
            CancellationToken.None);

        Assert.Equal(
            Path.Combine(_root, "threatfox", "fulls", "full_20260911T144900Z.json.zip"),
            saved.Path);
    }

    [Fact]
    public async Task Gzip_feed_keeps_its_compound_json_gzip_extension()
    {
        var store = CreateStore();

        var saved = await store.SaveAsync(
            "phishtank",
            RawFeedCategory.Full,
            "online-valid.json.gz",
            StreamOf("gzip-bytes"),
            CancellationToken.None);

        Assert.Equal(
            Path.Combine(_root, "phishtank", "fulls", "online-valid_20260911T144900Z.json.gz"),
            saved.Path);
    }

    [Fact]
    public async Task Binary_payload_is_preserved_byte_for_byte()
    {
        var store = CreateStore();
        var bytes = new byte[512];
        Random.Shared.NextBytes(bytes);

        var saved = await store.SaveAsync(
            "phishtank",
            RawFeedCategory.Full,
            "online-valid.json.gz",
            new MemoryStream(bytes),
            CancellationToken.None);

        Assert.Equal(bytes, await File.ReadAllBytesAsync(saved.Path));
        Assert.Equal(bytes.Length, saved.Length);
    }

    [Fact]
    public async Task OpenRead_returns_the_persisted_bytes()
    {
        var store = CreateStore();
        var saved = await store.SaveAsync(
            "threatfox",
            RawFeedCategory.Recent,
            "recent.json",
            StreamOf("[{\"id\":1}]"),
            CancellationToken.None);

        await using var stream = store.OpenRead(saved);
        using var reader = new StreamReader(stream);
        Assert.Equal("[{\"id\":1}]", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task Colliding_names_get_a_unique_suffix_instead_of_overwriting()
    {
        var store = CreateStore(basePath: _root);

        var first = await store.SaveAsync(
            "threatfox", RawFeedCategory.Recent, "recent.json", StreamOf("first"), CancellationToken.None);
        var second = await store.SaveAsync(
            "threatfox", RawFeedCategory.Recent, "recent.json", StreamOf("second"), CancellationToken.None);

        Assert.NotEqual(first.Path, second.Path);
        Assert.Equal("first", await File.ReadAllTextAsync(first.Path));
        Assert.Equal("second", await File.ReadAllTextAsync(second.Path));
    }

    [Fact]
    public async Task Directory_creation_failure_surfaces_as_a_transient_fetch_error()
    {
        File.WriteAllText(_root, "blocks the directory path");
        var store = CreateStore();

        var ex = await Assert.ThrowsAsync<TransientFetchException>(() => store.SaveAsync(
            "threatfox", RawFeedCategory.Recent, "recent.json", StreamOf("data"), CancellationToken.None));

        Assert.True(FetchErrorClassifier.IsTransient(ex));
        Assert.IsAssignableFrom<IOException>(ex.InnerException);
    }

    [Fact]
    public async Task Caller_cancellation_is_honored_before_writing()
    {
        var store = CreateStore();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.SaveAsync(
            "threatfox", RawFeedCategory.Recent, "recent.json", StreamOf("data"), cts.Token));
    }

    private static MemoryStream StreamOf(string value) => new(Encoding.UTF8.GetBytes(value));
}

internal sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow;
}
