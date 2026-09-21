using System.Text;
using ThreatIntelligence.Application.Fetches;
using ThreatIntelligence.Infrastructure.Configuration;
using ThreatIntelligence.Infrastructure.Storage;
using ThreatIntelligence.Infrastructure.ThreatFox;
using ThreatIntelligence.UnitTests.Storage;

namespace ThreatIntelligence.UnitTests.ThreatFox;

public sealed class PersistingThreatFoxExportClientTests : IDisposable
{
    private static readonly DateTimeOffset FixedNow = new(2026, 9, 11, 14, 49, 0, TimeSpan.Zero);

    private readonly string _root = Path.Combine(Path.GetTempPath(), "tieto-tf-" + Guid.NewGuid().ToString("N"));

    private RawFeedStore CreateStore(string? basePath = null)
        => new(new RawStorageOptions(basePath ?? _root), new FixedTimeProvider(FixedNow));

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
    public async Task Recent_fetch_is_persisted_then_served_from_the_saved_file()
    {
        var inner = new StubThreatFoxExportClient("recent.json", "[{\"id\":1}]");
        var client = new PersistingThreatFoxExportClient(inner, CreateStore());

        var result = await client.FetchRecentAsync(CancellationToken.None);

        Assert.Equal("recent.json", result.Name);
        var expectedPath = Path.Combine(_root, "threatfox", "recents", "recent_20260911T144900Z.json");
        Assert.True(File.Exists(expectedPath));

        using var reader = new StreamReader(result.Content);
        Assert.Equal("[{\"id\":1}]", await reader.ReadToEndAsync());
        Assert.True(inner.LastContent!.Disposed);
    }

    [Fact]
    public async Task Full_fetch_is_persisted_under_the_fulls_folder()
    {
        var inner = new StubThreatFoxExportClient("full.json.zip", "PK-zip-bytes");
        var client = new PersistingThreatFoxExportClient(inner, CreateStore());

        var result = await client.FetchFullAsync(CancellationToken.None);

        Assert.Equal("full.json.zip", result.Name);
        Assert.True(File.Exists(Path.Combine(_root, "threatfox", "fulls", "full_20260911T144900Z.json.zip")));

        using var reader = new StreamReader(result.Content);
        Assert.Equal("PK-zip-bytes", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task Store_failure_propagates_as_transient_and_disposes_the_network_stream()
    {
        var inner = new StubThreatFoxExportClient("recent.json", "[]");
        File.WriteAllText(_root, "blocks the directory path");
        var client = new PersistingThreatFoxExportClient(inner, CreateStore());

        var ex = await Assert.ThrowsAsync<TransientFetchException>(() =>
            client.FetchRecentAsync(CancellationToken.None));

        Assert.True(FetchErrorClassifier.IsTransient(ex));
        Assert.True(inner.LastContent!.Disposed);
    }

    private sealed class StubThreatFoxExportClient(string name, string content) : IThreatFoxExportClient
    {
        public TrackingStream? LastContent { get; private set; }

        public Task<ThreatFoxExportStream> FetchRecentAsync(CancellationToken cancellationToken)
            => Task.FromResult(Create());

        public Task<ThreatFoxExportStream> FetchFullAsync(CancellationToken cancellationToken)
            => Task.FromResult(Create());

        private ThreatFoxExportStream Create()
        {
            LastContent = new TrackingStream(Encoding.UTF8.GetBytes(content));
            return new ThreatFoxExportStream(name, LastContent);
        }
    }

    private sealed class TrackingStream(byte[] bytes) : MemoryStream(bytes)
    {
        public bool Disposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }
}
