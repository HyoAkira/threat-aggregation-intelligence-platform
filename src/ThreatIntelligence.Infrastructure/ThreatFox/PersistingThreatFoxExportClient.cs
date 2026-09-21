using ThreatIntelligence.Application.Fetches;

namespace ThreatIntelligence.Infrastructure.ThreatFox;

/// <summary>
/// Wraps <see cref="IThreatFoxExportClient"/> so every received export is first persisted to disk
/// via <see cref="IRawFeedStore"/>; the returned stream then reads from the saved file. This keeps
/// the executor contract (an incremental stream) unchanged while guaranteeing the original payload
/// is retained even if parsing later fails.
/// </summary>
public sealed class PersistingThreatFoxExportClient(
    IThreatFoxExportClient inner,
    IRawFeedStore rawFeedStore) : IThreatFoxExportClient
{
    internal const string ProviderSlug = "threatfox";

    public Task<ThreatFoxExportStream> FetchRecentAsync(CancellationToken cancellationToken)
        => FetchAndPersistAsync(RawFeedCategory.Recent, ct => inner.FetchRecentAsync(ct), cancellationToken);

    public Task<ThreatFoxExportStream> FetchFullAsync(CancellationToken cancellationToken)
        => FetchAndPersistAsync(RawFeedCategory.Full, inner.FetchFullAsync, cancellationToken);

    public Task<ThreatFoxExportStream> FetchRecentAsync(string? apiKey, CancellationToken cancellationToken)
        => FetchAndPersistAsync(RawFeedCategory.Recent, ct => inner.FetchRecentAsync(apiKey, ct), cancellationToken);

    public Task<ThreatFoxExportStream> FetchFullAsync(string? apiKey, CancellationToken cancellationToken)
        => FetchAndPersistAsync(RawFeedCategory.Full, ct => inner.FetchFullAsync(apiKey, ct), cancellationToken);

    private async Task<ThreatFoxExportStream> FetchAndPersistAsync(
        RawFeedCategory category,
        Func<CancellationToken, Task<ThreatFoxExportStream>> fetch,
        CancellationToken cancellationToken)
    {
        var stream = await fetch(cancellationToken).ConfigureAwait(false);

        await using (stream.Content)
        {
            var saved = await rawFeedStore
                .SaveAsync(ProviderSlug, category, stream.Name, stream.Content, cancellationToken)
                .ConfigureAwait(false);

            return new ThreatFoxExportStream(stream.Name, rawFeedStore.OpenRead(saved));
        }
    }
}
