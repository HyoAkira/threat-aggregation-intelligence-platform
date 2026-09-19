using System.Text.RegularExpressions;

namespace ThreatIntelligence.Infrastructure.AI;

internal static partial class IocChatAnswerValidator
{
    private static readonly string[] BoundaryMarkers =
    [
        "i can only help with",
        "i can help with public ioc",
        "i can help with cyber threat intelligence",
        "i cannot help with",
        "i can't help with",
        "i don't have enough information",
        "i do not have enough information",
        "i don't have access to",
        "i do not have access to",
        "i don't know",
        "i do not know",
        "outside the scope",
        "not related to cyber threat intelligence",
        "cannot answer that",
        "can't answer that",
        "cannot help with that",
        "can't help with that",
        "insufficient evidence",
        "the available evidence does not establish",
        "the database cannot establish",
        "i need more evidence",
        "i need clarification",
        "bu sohbet yalnızca",
        "bu sohbet sadece",
        "yalnızca siber tehdit istihbaratı",
        "sadece siber tehdit istihbaratı",
        "bu konuda yardımcı olamam",
        "bu konuda yardımcı olamayacağım",
        "bu soru kapsamım dışında",
        "kapsamım dışında",
        "yeterli kanıt yok",
        "yeterli delil yok",
        "veritabanı bu sonucu desteklemiyor",
        "veritabanı bu soruyu yanıtlayamıyor",
        "bu bilgi mevcut değil",
        "daha fazla kanıt gerekiyor",
        "sorunuzu netleştirir misiniz"
    ];

    public static void Validate(string answer, ISet<int> iocIds, ISet<string> recordIds)
    {
        if (PromptLeakPattern().IsMatch(answer))
            throw Invalid("The chat answer attempted to disclose protected instructions.");

        var hasAssessment = answer.Contains("Assessment:", StringComparison.OrdinalIgnoreCase);

        // Refusals, clarifications, insufficient-evidence responses, and factual evidence
        // summaries are intentionally natural language. Assessments remain explicitly labeled.
        if (!hasAssessment)
        {
            if (BoundaryMarkers.Any(marker => answer.Contains(marker, StringComparison.OrdinalIgnoreCase))) return;
            if (answer.Contains("Evidence:", StringComparison.OrdinalIgnoreCase) && HasRetrievedEvidence(answer, iocIds, recordIds)) return;
            throw Invalid("The chat model returned an unlabeled substantive answer.");
        }

        if (!answer.Contains("Evidence:", StringComparison.OrdinalIgnoreCase))
            throw Invalid("Evidence is required for a substantive IOC answer.");

        if (!HasRetrievedEvidence(answer, iocIds, recordIds))
            throw Invalid("The chat answer cited evidence that was not retrieved.");

    }

    private static IocChatException Invalid(string message) =>
        new("ioc_chat_invalid_answer", message, 503);

    private static bool HasRetrievedEvidence(string answer, ISet<int> iocIds, ISet<string> recordIds) =>
        iocIds.Any(id => answer.Contains($"#{id}", StringComparison.Ordinal)) ||
        recordIds.Any(record => answer.Contains(record, StringComparison.Ordinal));

    [GeneratedRegex("(system prompt|hidden instructions|developer message|reveal.*prompt|internal policy|api key|secret|source code)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PromptLeakPattern();
}
