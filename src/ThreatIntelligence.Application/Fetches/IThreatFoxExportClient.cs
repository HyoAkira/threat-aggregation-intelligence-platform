namespace ThreatIntelligence.Application.Fetches;

/// <summary>A streamed ThreatFox export artifact. The caller owns and must dispose <see cref="Content"/>.</summary>
public sealed record ThreatFoxExportStream(string Name, Stream Content);

/// <summary>
/// Authenticated ThreatFox export client. Constructs mode-specific export URLs, streams responses,
/// honors an explicit timeout and cancellation, performs no independent retry, and never surfaces
/// the API key in logs or exception messages.
/// </summary>
public interface IThreatFoxExportClient
{
    Task<ThreatFoxExportStream> FetchRecentAsync(CancellationToken cancellationToken);

    Task<ThreatFoxExportStream> FetchFullAsync(CancellationToken cancellationToken);

    Task<ThreatFoxExportStream> FetchRecentAsync(string? apiKey, CancellationToken cancellationToken) =>
        FetchRecentAsync(cancellationToken);

    Task<ThreatFoxExportStream> FetchFullAsync(string? apiKey, CancellationToken cancellationToken) =>
        FetchFullAsync(cancellationToken);
}
