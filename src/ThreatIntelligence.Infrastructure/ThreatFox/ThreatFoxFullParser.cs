using System.IO.Compression;
using System.Runtime.CompilerServices;
using ThreatIntelligence.Application.ProviderRecords;

namespace ThreatIntelligence.Infrastructure.ThreatFox;

/// <summary>
/// Streams the ThreatFox full <c>full.json.zip</c> export: locates the expected JSON entry by name,
/// enforces archive-bomb guards on entry count and declared uncompressed size, and parses the
/// contained JSON array without buffering the whole archive in memory.
/// </summary>
public sealed class ThreatFoxFullParser(ThreatFoxRecentParser recentParser)
{
    public const string EntryFileName = "full.json";
    public const int DefaultMaxEntries = 4;
    public const long DefaultMaxUncompressedBytes = 4L * 1024 * 1024 * 1024;

    private readonly int _maxEntries = DefaultMaxEntries;
    private readonly long _maxUncompressedBytes = DefaultMaxUncompressedBytes;

    public ThreatFoxFullParser(ThreatFoxRecentParser recentParser, int maxEntries, long maxUncompressedBytes)
        : this(recentParser)
    {
        if (maxEntries < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxEntries), "maxEntries must be positive.");
        }

        if (maxUncompressedBytes < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxUncompressedBytes), "maxUncompressedBytes must be positive.");
        }

        _maxEntries = maxEntries;
        _maxUncompressedBytes = maxUncompressedBytes;
    }

    public async IAsyncEnumerable<ProviderRecordInput> ParseAsync(
        Stream zipStream,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var archive = OpenArchive(zipStream);
        var entry = SelectEntry(archive);

        await using var entryStream = entry.Open();

        await foreach (var record in recentParser.ParseAsync(entryStream, cancellationToken))
        {
            yield return record;
        }
    }

    /// <summary>Opens the archive, mapping corrupt/truncated central-directory reads to a structured error.</summary>
    private static ZipArchive OpenArchive(Stream zipStream)
    {
        try
        {
            return new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: true);
        }
        catch (InvalidDataException ex)
        {
            throw new ThreatFoxArchiveException("Archive could not be read; it is corrupt or truncated.", ex);
        }
    }

    private ZipArchiveEntry SelectEntry(ZipArchive archive)
    {
        if (archive.Entries.Count == 0)
        {
            throw new ThreatFoxArchiveException("Archive does not contain any entries.");
        }

        if (archive.Entries.Count > _maxEntries)
        {
            throw new ThreatFoxArchiveException($"Archive contains {archive.Entries.Count} entries, exceeding the guard of {_maxEntries}.");
        }

        var entry = archive.Entries.FirstOrDefault(e =>
            string.Equals(e.Name, EntryFileName, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            throw new ThreatFoxArchiveException($"Archive does not contain the expected '{EntryFileName}' entry.");
        }

        if (entry.Length > _maxUncompressedBytes)
        {
            throw new ThreatFoxArchiveException($"Archive entry declares {entry.Length} uncompressed bytes, exceeding the guard of {_maxUncompressedBytes}.");
        }

        return entry;
    }
}