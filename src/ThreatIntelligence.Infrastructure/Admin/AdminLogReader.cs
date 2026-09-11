using System.Globalization;
using System.Text.Json;
using ThreatIntelligence.Application.Abstractions.Persistence;
using ThreatIntelligence.Application.Admin;
using ThreatIntelligence.Infrastructure.Configuration;

namespace ThreatIntelligence.Infrastructure.Admin;

/// <summary>
/// Reads the newest entries from the configured Serilog rolling files, newest file first and newest
/// line first. Missing/rotated files and malformed partial lines are skipped so a concurrent
/// rotation never fails the request. Results are capped before they are returned and every message
/// and exception is redacted.
/// </summary>
public sealed class AdminLogReader(AdminLogOptions options) : IAdminLogReader
{
    public async Task<IReadOnlyList<AdminLogEntry>> ReadAsync(
        int count,
        string? level,
        CancellationToken cancellationToken)
    {
        if (count < 1 || !Directory.Exists(options.Directory))
        {
            return [];
        }

        var entries = new List<AdminLogEntry>(count);
        foreach (var file in CandidateFiles())
        {
            cancellationToken.ThrowIfCancellationRequested();

            string[] lines;
            try
            {
                lines = await File.ReadAllLinesAsync(file, cancellationToken);
            }
            catch (IOException)
            {
                // The file rotated away between discovery and read.
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            for (var index = lines.Length - 1; index >= 0 && entries.Count < count; index--)
            {
                if (TryParse(lines[index], out var entry) && Matches(entry.Level, level))
                {
                    entries.Add(entry);
                }
            }

            if (entries.Count >= count)
            {
                break;
            }
        }

        return entries;
    }

    private IEnumerable<string> CandidateFiles()
    {
        try
        {
            return Directory.EnumerateFiles(options.Directory, options.FileNamePrefix + "*.json")
                .OrderByDescending(file => Path.GetFileName(file), StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (DirectoryNotFoundException)
        {
            return [];
        }
    }

    private static bool Matches(string entryLevel, string? requestedLevel) =>
        string.IsNullOrEmpty(requestedLevel)
        || string.Equals(entryLevel, requestedLevel, StringComparison.OrdinalIgnoreCase);

    private static bool TryParse(string line, out AdminLogEntry entry)
    {
        entry = null!;
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("@t", out var timestamp)
                || timestamp.ValueKind != JsonValueKind.String
                || !DateTimeOffset.TryParse(
                    timestamp.GetString(),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var parsedTimestamp)
                || !root.TryGetProperty("@l", out var level)
                || level.ValueKind != JsonValueKind.String
                || string.IsNullOrEmpty(level.GetString()))
            {
                return false;
            }

            var message = ReadString(root, "@m") ?? ReadString(root, "@mt");
            if (string.IsNullOrEmpty(message))
            {
                return false;
            }

            entry = new AdminLogEntry(
                parsedTimestamp,
                level.GetString()!,
                LogRedactor.Redact(message)!,
                LogRedactor.Redact(ReadString(root, "@x")));
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string? ReadString(JsonElement root, string propertyName) =>
        root.TryGetProperty(propertyName, out var element) && element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : null;
}
