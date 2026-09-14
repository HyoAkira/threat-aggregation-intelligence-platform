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
    {
        var feed = await inner.FetchFeedAsync(cancellationToken).ConfigureAwait(false);

        await using (feed.Content)
        {
            var saved = await rawFeedStore
                .SaveAsync(ProviderSlug, RawFeedCategory.Full, feed.Name, feed.Content, cancellationToken)
                .ConfigureAwait(false);

            return new PhishTankFeedStream(feed.Name, rawFeedStore.OpenRead(saved));
        }
    }
}
