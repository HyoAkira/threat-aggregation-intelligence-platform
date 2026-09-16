using System.Globalization;
using System.Text.Json;
using ThreatIntelligence.Application.Abstractions.Persistence;
using ThreatIntelligence.Application.Admin;
using ThreatIntelligence.Infrastructure.Configuration;

namespace ThreatIntelligence.Infrastructure.Admin;

/// <summary>
/// Reads the newest entries from the configured Serilog rolling files, newest file first and newest
/// line first. Message templates are rendered from their captured properties. Missing/rotated files
/// and malformed partial lines are skipped so a concurrent rotation never fails the request. Results
/// are capped before they are returned and every message and exception is redacted.
/// </summary>
public sealed class AdminLogReader(AdminLogOptions options) : IAdminLogReader
{
    public async Task<IReadOnlyList<AdminLogEntry>> ReadAsync(
        int count,
        string? level,
        string source,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        if (count < 1)
        {
            return [];
        }

        var entries = new List<AdminLogEntry>();
        foreach (var file in CandidateFiles(source))
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

            for (var index = lines.Length - 1; index >= 0; index--)
            {
                if (TryParse(lines[index], out var entry)
                    && Matches(entry.Level, level)
                    && MatchesCorrelation(entry.CorrelationId, correlationId))
                {
                    entries.Add(entry);
                }
            }
        }

        return entries
            .OrderByDescending(entry => entry.Timestamp)
            .Take(count)
            .ToList();
    }

    private IEnumerable<string> CandidateFiles(string source)
    {
        try
        {
            var workerDirectory = options.WorkerDirectory ?? options.Directory;
            (string Directory, string Prefix)[] files = source switch
            {
                "worker" => [(workerDirectory, options.WorkerFileNamePrefix)],
                "all" => [(options.Directory, options.FileNamePrefix), (workerDirectory, options.WorkerFileNamePrefix)],
                _ => [(options.Directory, options.FileNamePrefix)]
            };

            return files
                .Where(file => Directory.Exists(file.Directory))
                .SelectMany(file => Directory.EnumerateFiles(file.Directory, file.Prefix + "*.json"))
                .OrderByDescending(file => Path.GetFileName(file), StringComparer.OrdinalIgnoreCase)
                .ThenByDescending(file => file, StringComparer.OrdinalIgnoreCase)
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

    private static bool MatchesCorrelation(string? entryValue, string? requestedValue) =>
        string.IsNullOrWhiteSpace(requestedValue)
        || string.Equals(entryValue, requestedValue.Trim(), StringComparison.OrdinalIgnoreCase);

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
                    out var parsedTimestamp))
            {
                return false;
            }

            // CLEF omits @l when the level is the default (Information).
            string entryLevel;
            if (root.TryGetProperty("@l", out var level))
            {
                if (level.ValueKind != JsonValueKind.String || string.IsNullOrEmpty(level.GetString()))
                {
                    return false;
                }

                entryLevel = level.GetString()!;
            }
            else
            {
                entryLevel = "Information";
            }

            // CLEF omits @m unless the formatter pre-rendered the message; otherwise @mt is the
            // template and must be filled in from the properties on the same line.
            var template = ReadString(root, "@mt");
            var message = ReadString(root, "@m") ?? (template is null ? null : ClefMessageRenderer.Render(root, template));
            if (string.IsNullOrEmpty(message))
            {
                return false;
            }

            entry = new AdminLogEntry(
                parsedTimestamp,
                entryLevel,
                LogRedactor.Redact(ReadString(root, "CorrelationId")),
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
