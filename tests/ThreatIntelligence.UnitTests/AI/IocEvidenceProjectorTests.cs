using System.Text.Json;
using ThreatIntelligence.Application.AI;
using ThreatIntelligence.Application.Features.Iocs.Queries;
using Xunit;

namespace ThreatIntelligence.UnitTests.AI;

public sealed class IocEvidenceProjectorTests
{
    private static IocSourceResult Source(
        string provider,
        string recordId,
        int? port = null,
        string? threatType = "botnet",
        IReadOnlyList<string>? tags = null,
        bool isFalsePositive = false,
        JsonElement? details = null)
    {
        var now = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);
        return new IocSourceResult(
            provider, recordId, $"https://details.example/{recordId}", port, now, now.AddDays(1),
            threatType, "win.agenttesla", "agenttesla", "AgentTesla", true, "reporter-a", true,
            true, "evil.example", details, null, 90, tags ?? ["botnet", "credential_stealer"],
            isFalsePositive);
    }

    [Fact]
    public void Projects_deterministic_s1_sn_labels_with_provider_attribution()
    {
        var group = new IocGroupResult(
            7, "evil.example", "domain", "evil.example", "evil.example",
            new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 7, 1, 8, 0, 0, DateTimeKind.Utc),
            [
                Source("ThreatFox", "tf-2"),
                Source("PhishTank", "pt-9"),
                Source("ThreatFox", "tf-1"),
            ]);

        var snapshot = IocEvidenceProjector.Project(group);

        Assert.Equal(["S1", "S2", "S3"], snapshot.Sources.Select(source => source.Label).ToList());
        // Sources sort deterministically by provider then record id regardless of input order.
        Assert.Equal(["PhishTank", "ThreatFox", "ThreatFox"], snapshot.Sources.Select(source => source.Provider).ToList());
        Assert.Equal("pt-9", snapshot.Sources[0].ProviderRecordId);
        Assert.Equal("tf-1", snapshot.Sources[1].ProviderRecordId);
        Assert.Equal("tf-2", snapshot.Sources[2].ProviderRecordId);
        Assert.Equal(0, snapshot.OmittedSourceCount);
        // No provider links or reporter fields are projected.
        Assert.DoesNotContain("https://details.example", JsonSerializer.Serialize(snapshot, AiReportJson.Options));
        Assert.DoesNotContain("reporter-a", JsonSerializer.Serialize(snapshot, AiReportJson.Options));
    }

    [Fact]
    public void Port_variants_become_separate_sources()
    {
        var group = new IocGroupResult(
            3, "1.2.3.4", "ipv4", null, null,
            new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc), null,
            [Source("ThreatFox", "tf-a", 443), Source("ThreatFox", "tf-b", 8080)]);

        var snapshot = IocEvidenceProjector.Project(group);

        Assert.Equal(2, snapshot.Sources.Count);
        Assert.Equal(443, snapshot.Sources[0].Port);
        Assert.Equal(8080, snapshot.Sources[1].Port);
    }

    [Fact]
    public void False_positive_state_is_carried_per_source()
    {
        var group = new IocGroupResult(
            5, "evil.example", "domain", null, null,
            DateTime.UtcNow, null,
            [Source("ThreatFox", "tf-1", isFalsePositive: false), Source("PhishTank", "pt-2", isFalsePositive: true)]);

        var snapshot = IocEvidenceProjector.Project(group);

        Assert.False(snapshot.Sources.Single(source => source.Provider == "ThreatFox").IsFalsePositive);
        Assert.True(snapshot.Sources.Single(source => source.Provider == "PhishTank").IsFalsePositive);
    }

    [Fact]
    public void Phishtank_details_project_as_bounded_typed_lines()
    {
        var details = JsonDocument.Parse(
            """{"url":"http://one.example","in_db":true,"phish_detail_id":"pd-1","second":"value"}""").RootElement;
        var group = new IocGroupResult(
            9, "evil.example", "url", "evil.example", "evil.example",
            DateTime.UtcNow, null,
            [Source("PhishTank", "pt-1", details: details)]);

        var snapshot = IocEvidenceProjector.Project(group);

        Assert.Equal(4, snapshot.Sources[0].PhishTankDetailLines.Count);
        Assert.All(snapshot.Sources[0].PhishTankDetailLines, line => Assert.StartsWith("\"", line));
    }

    [Fact]
    public void Oversized_group_is_truncated_with_explicit_omitted_count()
    {
        var sources = Enumerable.Range(0, 40)
            .Select(index => Source("ThreatFox", $"tf-{index:D2}"))
            .ToList();
        var group = new IocGroupResult(11, "evil.example", "url", null, null, DateTime.UtcNow, null, sources);

        var snapshot = IocEvidenceProjector.Project(group, maxSources: 4, maxTotalBytes: 100_000);

        Assert.Equal(4, snapshot.Sources.Count);
        Assert.Equal(36, snapshot.OmittedSourceCount);
        Assert.Equal("S4", snapshot.Sources[^1].Label);
        var serialized = JsonSerializer.Serialize(snapshot, AiReportJson.Options);
        Assert.True(System.Text.Encoding.UTF8.GetByteCount(serialized) <= 100_000);
    }

    [Fact]
    public void Byte_budget_truncates_and_records_omissions()
    {
        var tags = new List<string>();
        var sources = Enumerable.Range(0, 10)
            .Select(index => Source("ThreatFox", new string('x', 2000) + index))
            .ToList();
        var group = new IocGroupResult(12, "evil.example", "url", null, null, DateTime.UtcNow, null, sources);

        var snapshot = IocEvidenceProjector.Project(group, maxSources: 24, maxTotalBytes: 4000);

        Assert.True(snapshot.Sources.Count < 10);
        Assert.Equal(10 - snapshot.Sources.Count, snapshot.OmittedSourceCount);
    }

    [Fact]
    public void Identical_input_yields_identical_serialization()
    {
        var group = new IocGroupResult(
            7, "evil.example", "url", "evil.example", "evil.example",
            new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc), null,
            [Source("PhishTank", "pt-2"), Source("ThreatFox", "tf-1")]);

        var first = JsonSerializer.Serialize(IocEvidenceProjector.Project(group), AiReportJson.Options);
        var second = JsonSerializer.Serialize(IocEvidenceProjector.Project(group), AiReportJson.Options);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Long_strings_are_truncated()
    {
        var longTag = new string('t', 500);
        var group = new IocGroupResult(
            2, "evil.example", "url", null, null, DateTime.UtcNow, null,
            [Source("ThreatFox", "tf-1", tags: [longTag])]);

        var snapshot = IocEvidenceProjector.Project(group);

        Assert.All(snapshot.Sources[0].Tags, tag => Assert.True(tag.Length <= IocEvidenceProjector.MaxTagLength));
    }
}
