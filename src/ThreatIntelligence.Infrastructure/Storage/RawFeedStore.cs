using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ThreatIntelligence.Application.Fetches;
using ThreatIntelligence.Infrastructure.Configuration;

namespace ThreatIntelligence.Infrastructure.Storage;

/// <summary>
/// Writes provider feeds to <c>{BasePath}/{provider}/{recents|fulls}/{stem}_{timestamp}{ext}</c>.
/// Files are written to a sibling <c>.tmp</c> file and renamed into place so a failed or
/// cancelled write never leaves a partial feed. Storage failures are wrapped in
/// <see cref="TransientFetchException"/>.
/// </summary>
public sealed class RawFeedStore : IRawFeedStore
{
    private const string RecentFolder = "recents";
    private const string FullFolder = "fulls";
    private const int CopyBufferSize = 81920;

    private static readonly string[] CompoundExtensions = [".json.gz", ".json.zip"];

    private readonly string _basePath;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<RawFeedStore> _logger;

    public RawFeedStore(
        RawStorageOptions options,
        TimeProvider timeProvider,
        ILogger<RawFeedStore>? logger = null)
    {
        _basePath = options.BasePath;
        _timeProvider = timeProvider;
        _logger = logger ?? NullLogger<RawFeedStore>.Instance;
    }

    public async Task<RawFeedFile> SaveAsync(
        string providerSlug,
        RawFeedCategory category,
        string sourceFileName,
        Stream content,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerSlug);
        ArgumentNullException.ThrowIfNull(content);
        cancellationToken.ThrowIfCancellationRequested();

        if (providerSlug.Contains(Path.DirectorySeparatorChar)
            || providerSlug.Contains(Path.AltDirectorySeparatorChar)
            || providerSlug.Contains("..", StringComparison.Ordinal))
        {
            throw new ArgumentException("Provider slug must be a single path segment.", nameof(providerSlug));
        }

        var directory = Path.Combine(Path.GetFullPath(_basePath), providerSlug, FolderFor(category));
        var fileName = BuildFileName(sourceFileName);

        try
        {
            Directory.CreateDirectory(directory);
            var finalPath = EnsureUnique(Path.Combine(directory, fileName));
            var tempPath = finalPath + ".tmp";

            try
            {
                await using (var destination = new FileStream(
                    tempPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    CopyBufferSize,
                    FileOptions.Asynchronous | FileOptions.SequentialScan))
                {
                    await content.CopyToAsync(destination, CopyBufferSize, cancellationToken).ConfigureAwait(false);
                }

                File.Move(tempPath, finalPath);
            }
            catch
            {
                TryDelete(tempPath);
                throw;
            }

            var length = new FileInfo(finalPath).Length;
            _logger.LogInformation(
                "Raw feed persisted Provider={Provider} Category={Category} Path={Path} Bytes={Bytes}",
                providerSlug,
                category,
                finalPath,
                length);

            return new RawFeedFile(providerSlug, category, Path.GetFileName(finalPath), finalPath, length);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or System.Security.SecurityException)
        {
            throw new TransientFetchException(
                $"Failed to persist raw {providerSlug} {category} feed to disk.",
                ex);
        }
    }

    public Stream OpenRead(RawFeedFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        return new FileStream(
            file.Path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            CopyBufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
    }

    private static string FolderFor(RawFeedCategory category) => category switch
    {
        RawFeedCategory.Recent => RecentFolder,
        RawFeedCategory.Full => FullFolder,
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, null)
    };

    private string BuildFileName(string sourceFileName)
    {
        var (stem, extension) = SplitExtension(sourceFileName);
        var timestamp = _timeProvider.GetUtcNow().ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
        return $"{stem}_{timestamp}{extension}";
    }

    private static (string Stem, string Extension) SplitExtension(string sourceFileName)
    {
        var name = Path.GetFileName(sourceFileName);

        foreach (var compound in CompoundExtensions)
        {
            if (name.EndsWith(compound, StringComparison.OrdinalIgnoreCase))
            {
                return (name[..^compound.Length], compound);
            }
        }

        var extension = Path.GetExtension(name);
        return extension.Length == 0
            ? (name, string.Empty)
            : (name[..^extension.Length], extension);
    }

    private static string EnsureUnique(string path)
    {
        if (!File.Exists(path))
        {
            return path;
        }

        var directory = Path.GetDirectoryName(path)!;
        var stem = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);

        for (var suffix = 1; ; suffix++)
        {
            var candidate = Path.Combine(directory, $"{stem}_{suffix}{extension}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best-effort cleanup; the original failure is what matters.
        }
    }
}
