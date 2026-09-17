using System.Text.RegularExpressions;

namespace ThreatIntelligence.Application.AI;

/// <summary>
/// Validates a completed report before the server emits its terminal event. Validation enforces
/// the required Turkish sections, bounded size, and that every cited source label exists in the
/// evidence snapshot. Invented labels or missing sections fail the run.
/// </summary>
public static partial class AiReportOutputValidator
{
    public const int MaxReportChars = 24_000;

    private static readonly string[] RequiredSections =
    [
        "## Yönetici Özeti",
        "## Teknik Analiz",
        "## Bağlam",
        "## Öneriler",
        "## Sınırlamalar ve Belirsizlik",
        "## Feragatname",
    ];

    [GeneratedRegex("(?<![A-Za-z])S(?<index>[0-9]{1,3})(?![0-9])", RegexOptions.CultureInvariant)]
    private static partial Regex SourceLabelPattern();

    public static void Validate(string report, IReadOnlyCollection<string> availableLabels)
    {
        if (string.IsNullOrWhiteSpace(report))
        {
            throw new AiOutputInvalidException("The model returned an empty report.");
        }

        if (report.Length > MaxReportChars)
        {
            throw new AiOutputInvalidException(
                $"The report exceeds the {MaxReportChars.ToString(System.Globalization.CultureInfo.InvariantCulture)} character limit.");
        }

        var missing = RequiredSections
            .Where(section => !report.Contains(section, StringComparison.Ordinal))
            .ToList();
        if (missing.Count > 0)
        {
            throw new AiOutputInvalidException(
                $"The report is missing required sections: {string.Join(", ", missing)}.");
        }

        var cited = SourceLabelPattern()
            .Matches(report)
            .Select(match => $"S{match.Groups["index"].Value}")
            .Distinct()
            .ToList();

        if (availableLabels.Count == 0 && cited.Count > 0)
        {
            throw new AiOutputInvalidException("The report cites sources but the evidence snapshot has none.");
        }

        var invented = cited.Except(availableLabels).ToList();
        if (invented.Count > 0)
        {
            throw new AiOutputInvalidException(
                $"The report cites source labels that are not in the evidence snapshot: {string.Join(", ", invented)}.");
        }
    }
}
