using ThreatIntelligence.Application.Fetches;

namespace ThreatIntelligence.Infrastructure.PhishTank;

/// <summary>
/// Wraps <see cref="IPhishTankClient"/> so the downloaded gzip feed is first persisted to disk via
/// <see cref="IRawFeedStore"/>; the returned stream then reads from the saved file.
/// </summary>
public sealed class PersistingPhishTankClient(
    IPhishTankClient inner,
    IRawFeedStore rawFeedStore) : IPhishTankClient
{
    internal const string ProviderSlug = "phishtank";

    public async Task<PhishTankFeedStream> FetchFeedAsync(CancellationToken cancellationToken)
        => await PersistAsync(
            inner.FetchFeedAsync(cancellationToken),
            cancellationToken).ConfigureAwait(false);

    public async Task<PhishTankFeedStream> FetchFeedAsync(string? appKey, CancellationToken cancellationToken)
        => await PersistAsync(
            inner.FetchFeedAsync(appKey, cancellationToken),
            cancellationToken).ConfigureAwait(false);

    private async Task<PhishTankFeedStream> PersistAsync(
        Task<PhishTankFeedStream> feedTask,
        CancellationToken cancellationToken)
    {
        var feed = await feedTask.ConfigureAwait(false);

        await using (feed.Content)
        {
            var saved = await rawFeedStore
                .SaveAsync(ProviderSlug, RawFeedCategory.Full, feed.Name, feed.Content, cancellationToken)
                .ConfigureAwait(false);

            return new PhishTankFeedStream(feed.Name, rawFeedStore.OpenRead(saved));
        }
    }
}
