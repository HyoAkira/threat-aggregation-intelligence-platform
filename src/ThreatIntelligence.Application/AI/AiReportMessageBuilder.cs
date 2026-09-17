using System.Text.Encodings.Web;
using System.Text.Json;

namespace ThreatIntelligence.Application.AI;

/// <summary>
/// Builds the version-pinned chat messages for one report run from fixed safety instructions, the
/// active Admin policy document, and the request-scoped evidence snapshot.
/// </summary>
public static class AiReportMessageBuilder
{
    public static IReadOnlyList<ChatCompletionMessage> Build(
        AiReportPolicySnapshot policy,
        IocEvidenceSnapshot evidence)
    {
        var evidenceJson = JsonSerializer.Serialize(evidence, AiReportJson.Options);
        var policyText = $"""
            REPORT POLICY (version {policy.Version.ToString(System.Globalization.CultureInfo.InvariantCulture)}):
            {policy.Text}
            """;

        return
        [
            new("system", policyText),
            new("system", FixedSafetyInstructions.Text),
            new("user", $"""
                IOC evidence snapshot (source labels {string.Join(", ", IocEvidenceProjector.SourceLabels(evidence))}{(evidence.OmittedSourceCount > 0 ? $"; {evidence.OmittedSourceCount} sources omitted" : string.Empty)}):
                {evidenceJson}
                """),
        ];
    }
}
