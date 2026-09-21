using System.Text;
using ThreatIntelligence.Application.Fetches;
using ThreatIntelligence.Infrastructure.Configuration;
using ThreatIntelligence.Infrastructure.PhishTank;
using ThreatIntelligence.Infrastructure.Storage;
using ThreatIntelligence.UnitTests.Storage;

namespace ThreatIntelligence.UnitTests.PhishTank;

public sealed class PersistingPhishTankClientTests : IDisposable
{
    private static readonly DateTimeOffset FixedNow = new(2026, 9, 11, 14, 49, 0, TimeSpan.Zero);

    private readonly string _root = Path.Combine(Path.GetTempPath(), "tieto-pt-" + Guid.NewGuid().ToString("N"));

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
    public async Task Feed_is_persisted_under_phishtank_fulls_then_served_from_the_saved_file()
    {
        var inner = new StubPhishTankClient("online-valid.json.gz", "gzip-bytes");
        var client = new PersistingPhishTankClient(inner, CreateStore());

        var result = await client.FetchFeedAsync(CancellationToken.None);

        Assert.Equal("online-valid.json.gz", result.Name);
        var expectedPath = Path.Combine(_root, "phishtank", "fulls", "online-valid_20260911T144900Z.json.gz");
        Assert.True(File.Exists(expectedPath));

        using var reader = new StreamReader(result.Content);
        Assert.Equal("gzip-bytes", await reader.ReadToEndAsync());
        Assert.True(inner.LastContent!.Disposed);
    }

    [Fact]
    public async Task Store_failure_propagates_as_transient_and_disposes_the_network_stream()
    {
        var inner = new StubPhishTankClient("online-valid.json.gz", "gzip");
        File.WriteAllText(_root, "blocks the directory path");
        var client = new PersistingPhishTankClient(inner, CreateStore());

        var ex = await Assert.ThrowsAsync<TransientFetchException>(() =>
            client.FetchFeedAsync(CancellationToken.None));

        Assert.True(FetchErrorClassifier.IsTransient(ex));
        Assert.True(inner.LastContent!.Disposed);
    }

    private sealed class StubPhishTankClient(string name, string content) : IPhishTankClient
    {
        public TrackingStream? LastContent { get; private set; }

        public Task<PhishTankFeedStream> FetchFeedAsync(CancellationToken cancellationToken)
        {
            LastContent = new TrackingStream(Encoding.UTF8.GetBytes(content));
            return Task.FromResult(new PhishTankFeedStream(name, LastContent));
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
