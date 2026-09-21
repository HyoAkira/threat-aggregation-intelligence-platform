namespace ThreatIntelligence.Application.Fetches;

/// <summary>The kind of provider feed being persisted. Maps to the on-disk folder name.</summary>
public enum RawFeedCategory : byte
{
    Recent = 0,
    Full = 1
}

/// <summary>A raw provider feed artifact that has been persisted to disk.</summary>
public sealed record RawFeedFile(
    string ProviderSlug,
    RawFeedCategory Category,
    string FileName,
    string Path,
    long Length);

/// <summary>
/// Persists provider feeds exactly as received so operators can replay or audit them.
/// Implementations write the original bytes to disk and surface storage failures as
/// <see cref="TransientFetchException"/> so the fetch policy can retry.
/// </summary>
public interface IRawFeedStore
{
    Task<RawFeedFile> SaveAsync(
        string providerSlug,
        RawFeedCategory category,
        string sourceFileName,
        Stream content,
        CancellationToken cancellationToken);

    Stream OpenRead(RawFeedFile file);
}
