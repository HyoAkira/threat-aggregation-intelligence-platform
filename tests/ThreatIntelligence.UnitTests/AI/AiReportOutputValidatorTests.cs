using ThreatIntelligence.Application.AI;
using Xunit;

namespace ThreatIntelligence.UnitTests.AI;

public sealed class AiReportOutputValidatorTests
{
    private static string ReportWith(params string[] labels) => $"""
        ## Yönetici Özeti
        Özet.
        ## Teknik Analiz
        Gözlem {string.Join(' ', labels.Select(label => $"[{label}]"))}.
        ## Bağlam
        Bağlam.
        ## Öneriler
        Öneri.
        ## Sınırlamalar ve Belirsizlik
        Sınırlama.
        ## Feragatname
        Feragat.
        """;

    [Fact]
    public void Valid_report_with_known_labels_passes()
    {
        AiReportOutputValidator.Validate(ReportWith("S1", "S2"), ["S1", "S2", "S3"]);
    }

    [Fact]
    public void Invented_source_label_fails_validation()
    {
        var exception = Record.Exception(() =>
            AiReportOutputValidator.Validate(ReportWith("S1", "S9"), ["S1", "S2", "S3"]));

        Assert.IsType<AiOutputInvalidException>(exception);
        Assert.Contains("S9", ((AiOutputInvalidException)exception).Detail);
    }

    [Fact]
    public void Missing_required_section_fails_validation()
    {
        var report = ReportWith("S1").Replace("## Feragatname", "## Disclaimer");

        var exception = Record.Exception(() => AiReportOutputValidator.Validate(report, ["S1"]));

        Assert.IsType<AiOutputInvalidException>(exception);
        Assert.Contains("Feragatname", ((AiOutputInvalidException)exception).Detail);
    }

    [Fact]
    public void Empty_report_fails_validation()
    {
        Assert.Throws<AiOutputInvalidException>(
            () => AiReportOutputValidator.Validate("   ", ["S1"]));
    }

    [Fact]
    public void Oversized_report_fails_validation()
    {
        var report = ReportWith("S1") + new string('a', AiReportOutputValidator.MaxReportChars);

        var exception = Record.Exception(() => AiReportOutputValidator.Validate(report, ["S1"]));

        Assert.IsType<AiOutputInvalidException>(exception);
        Assert.Contains("limit", ((AiOutputInvalidException)exception).Detail);
    }

    [Fact]
    public void Sparse_evidence_allows_reports_without_labels_but_rejects_labels_when_none_exist()
    {
        AiReportOutputValidator.Validate(
            ReportWith(), availableLabels: []);

        var exception = Record.Exception(() =>
            AiReportOutputValidator.Validate(ReportWith("S1"), availableLabels: []));

        Assert.IsType<AiOutputInvalidException>(exception);
    }

    [Fact]
    public void Messages_include_policy_version_safety_rules_and_evidence()
    {
        var policy = new AiReportPolicySnapshot(3, "Türkçe yönerge");
        var evidence = new IocEvidenceSnapshot(
            7, "evil.example", "url", "evil.example", "evil.example",
            "2026-01-01T08:00:00Z", null, [], 2);

        var messages = AiReportMessageBuilder.Build(policy, evidence);
        var joined = string.Join('\n', messages.Select(message => $"{message.Role}: {message.Content}"));

        Assert.Equal(3, messages.Count);
        Assert.Contains("version 3", joined);
        Assert.Contains("Türkçe yönerge", joined);
        Assert.Contains("SAFETY AND SCOPE RULES", joined);
        Assert.Contains("2 sources omitted", joined);
        Assert.Contains("\"iocValue\":\"evil.example\"", joined);
    }
}
