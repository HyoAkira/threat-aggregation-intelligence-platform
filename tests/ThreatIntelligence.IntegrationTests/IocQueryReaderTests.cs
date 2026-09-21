using ThreatIntelligence.Application.BatchWriter;
using ThreatIntelligence.Application.Features.Iocs.Queries;
using ThreatIntelligence.Application.Normalization;
using ThreatIntelligence.Domain.Enums;
using ThreatIntelligence.Infrastructure.Hashing;
using ThreatIntelligence.Infrastructure.Persistence;
using ThreatIntelligence.Infrastructure.Persistence.Repositories;

namespace ThreatIntelligence.IntegrationTests;

[Collection("sqlserver")]
public sealed class IocQueryReaderTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private static readonly DateTime Now = new(2026, 9, 10, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Recent = Now.AddDays(-10);
    private readonly FixedTimeProvider _clock = new(new DateTimeOffset(Now, TimeSpan.Zero));

    public Task InitializeAsync() => fixture.ResetDataAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private IocQueryReader CreateReader(ThreatIntelligenceDbContext context) =>
        new(context, new Sha256ValueHasher(), new FalsePositiveEvaluator(_clock));

    // --- 1.2 / 1.3 Search -------------------------------------------------------

    [Fact]
    public async Task Search_bare_ip_groups_port_variants_and_providers()
    {
        await using var context = fixture.CreateContext();
        await IocQueryTestData.InsertIocAsync(context, 1, "s-1", "1.2.3.4", IocType.Ipv4, lastSeenUtc: Recent);
        await IocQueryTestData.InsertIocAsync(context, 1, "s-2", "1.2.3.4", IocType.Ipv4, port: 443, lastSeenUtc: Recent);
        await IocQueryTestData.InsertIocAsync(context, 2, "s-3", "1.2.3.4", IocType.Ipv4, lastSeenUtc: Recent);

        var reader = CreateReader(fixture.CreateContext());
        var result = await reader.SearchGroupsAsync(
            new NormalizationResult(IocType.Ipv4, "1.2.3.4", null, null, null),
            new IocSourceFilter(),
            1,
            20,
            CancellationToken.None);

        var group = Assert.Single(result.Items);
        Assert.Equal("ipv4", group.IocType);
        Assert.Equal(3, group.Sources.Count);
        Assert.Contains(group.Sources, source => source.Provider == "ThreatFox" && source.Port == 443);
        Assert.Contains(group.Sources, source => source.Provider == "PhishTank");
    }

    [Fact]
    public async Task Search_port_bearing_ip_matches_only_that_port()
    {
        await using var context = fixture.CreateContext();
        await IocQueryTestData.InsertIocAsync(context, 1, "p-1", "5.6.7.8", IocType.Ipv4);
        await IocQueryTestData.InsertIocAsync(context, 1, "p-2", "5.6.7.8", IocType.Ipv4, port: 443);

        var reader = CreateReader(fixture.CreateContext());
        var result = await reader.SearchGroupsAsync(
            new NormalizationResult(IocType.Ipv4, "5.6.7.8", 443, null, null),
            new IocSourceFilter(),
            1,
            20,
            CancellationToken.None);

        var group = Assert.Single(result.Items);
        Assert.Single(group.Sources);
        Assert.Equal(443, group.Sources[0].Port);
    }

    [Fact]
    public async Task Search_tag_filter_narrows_sources_and_keeps_attribution()
    {
        await using var context = fixture.CreateContext();
        var tagged = await IocQueryTestData.InsertIocAsync(context, 1, "t-1", "6.6.6.6", IocType.Ipv4);
        await IocQueryTestData.InsertIocAsync(context, 2, "t-2", "6.6.6.6", IocType.Ipv4);
        await IocQueryTestData.TagAsync(context, tagged, " BotNet ");

        var reader = CreateReader(fixture.CreateContext());
        var result = await reader.SearchGroupsAsync(
            new NormalizationResult(IocType.Ipv4, "6.6.6.6", null, null, null),
            new IocSourceFilter("botnet"),
            1,
            20,
            CancellationToken.None);

        var group = Assert.Single(result.Items);
        var source = Assert.Single(group.Sources);
        Assert.Equal("ThreatFox", source.Provider);
        Assert.Equal(["botnet"], source.Tags);
    }

    [Fact]
    public async Task Search_provider_filter_narrows_sources()
    {
        await using var context = fixture.CreateContext();
        await IocQueryTestData.InsertIocAsync(context, 1, "pv-1", "7.7.7.7", IocType.Ipv4);
        await IocQueryTestData.InsertIocAsync(context, 2, "pv-2", "7.7.7.7", IocType.Ipv4);

        var reader = CreateReader(fixture.CreateContext());
        var result = await reader.SearchGroupsAsync(
            new NormalizationResult(IocType.Ipv4, "7.7.7.7", null, null, null),
            new IocSourceFilter(Provider: "1"),
            1,
            20,
            CancellationToken.None);

        var group = Assert.Single(result.Items);
        Assert.Single(group.Sources);
        Assert.Equal("ThreatFox", group.Sources[0].Provider);
    }

    [Fact]
    public async Task Search_malware_filter_is_case_insensitive_and_narrows_sources()
    {
        await using var context = fixture.CreateContext();
        await IocQueryTestData.InsertIocAsync(
            context, 1, "mw-1", "6.7.8.9", IocType.Ipv4, malware: "Emotet");
        await IocQueryTestData.InsertIocAsync(
            context, 2, "mw-2", "6.7.8.9", IocType.Ipv4, malware: "TrickBot");

        var reader = CreateReader(fixture.CreateContext());
        var result = await reader.SearchGroupsAsync(
            new NormalizationResult(IocType.Ipv4, "6.7.8.9", null, null, null),
            new IocSourceFilter(Malware: "  EMOTET "),
            1,
            20,
            CancellationToken.None);

        var group = Assert.Single(result.Items);
        var source = Assert.Single(group.Sources);
        Assert.Equal("Emotet", source.Malware);
    }

    [Fact]
    public async Task Search_target_filter_composes_with_provider()
    {
        await using var context = fixture.CreateContext();
        await IocQueryTestData.InsertIocAsync(
            context, 1, "tf-1", "6.7.8.10", IocType.Ipv4, target: "Amazon");
        await IocQueryTestData.InsertIocAsync(
            context, 2, "tf-2", "6.7.8.10", IocType.Ipv4, target: "Amazon");
        await IocQueryTestData.InsertIocAsync(
            context, 1, "tf-3", "6.7.8.10", IocType.Ipv4, port: 8443, target: "Contoso");

        var reader = CreateReader(fixture.CreateContext());
        var result = await reader.SearchGroupsAsync(
            new NormalizationResult(IocType.Ipv4, "6.7.8.10", null, null, null),
            new IocSourceFilter(Provider: "1", Target: "amazon"),
            1,
            20,
            CancellationToken.None);

        var group = Assert.Single(result.Items);
        var source = Assert.Single(group.Sources);
        Assert.Equal("Amazon", source.Target);
    }

    [Fact]
    public async Task Search_filter_that_removes_every_source_returns_null()
    {
        await using var context = fixture.CreateContext();
        await IocQueryTestData.InsertIocAsync(context, 1, "f-1", "8.8.8.8", IocType.Ipv4);

        var reader = CreateReader(fixture.CreateContext());
        var result = await reader.SearchGroupsAsync(
            new NormalizationResult(IocType.Ipv4, "8.8.8.8", null, null, null),
            new IocSourceFilter("does-not-exist"),
            1,
            20,
            CancellationToken.None);

        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task Search_malware_filter_without_a_match_returns_empty()
    {
        await using var context = fixture.CreateContext();
        await IocQueryTestData.InsertIocAsync(
            context, 1, "mw-none", "8.8.4.4", IocType.Ipv4, malware: "Emotet");

        var reader = CreateReader(fixture.CreateContext());
        var result = await reader.SearchGroupsAsync(
            new NormalizationResult(IocType.Ipv4, "8.8.4.4", null, null, null),
            new IocSourceFilter(Malware: "Qakbot"),
            1,
            20,
            CancellationToken.None);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task Search_absent_indicator_returns_null()
    {
        var reader = CreateReader(fixture.CreateContext());
        var result = await reader.SearchGroupsAsync(
            new NormalizationResult(IocType.Domain, "absent.example", null, null, "absent.example"),
            new IocSourceFilter(),
            1,
            20,
            CancellationToken.None);

        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task Search_domain_finds_exact_url_host_and_parent_domain_groups()
    {
        await using var context = fixture.CreateContext();
        await IocQueryTestData.InsertIocAsync(
            context, 1, "host-1", "https://shop.example.co.uk/a", IocType.Url,
            urlHost: "shop.example.co.uk", parentDomain: "example.co.uk");
        await IocQueryTestData.InsertIocAsync(
            context, 1, "host-2", "https://shop.example.co.uk/b", IocType.Url,
            port: 8443, urlHost: "shop.example.co.uk", parentDomain: "example.co.uk");
        await IocQueryTestData.InsertIocAsync(
            context, 1, "host-3", "api.example.co.uk", IocType.Domain,
            parentDomain: "example.co.uk");

        var reader = CreateReader(fixture.CreateContext());

        var hostResult = await reader.SearchGroupsAsync(
            new NormalizationResult(
                IocType.Domain, "shop.example.co.uk", null, null, "example.co.uk"),
            new IocSourceFilter(),
            1,
            20,
            CancellationToken.None);
        Assert.Equal(2, hostResult.TotalCount);
        Assert.All(hostResult.Items, item => Assert.Equal("shop.example.co.uk", item.UrlHost));

        var parentResult = await reader.SearchGroupsAsync(
            new NormalizationResult(IocType.Domain, "example.co.uk", null, null, "example.co.uk"),
            new IocSourceFilter(),
            1,
            20,
            CancellationToken.None);
        Assert.Equal(3, parentResult.TotalCount);
        Assert.Contains(parentResult.Items, item => item.IocValue == "api.example.co.uk");
        Assert.Contains(parentResult.Items, item => item.IocValue == "https://shop.example.co.uk/a");
    }

    // --- 1.4 Detail -------------------------------------------------------------

    [Fact]
    public async Task Detail_returns_full_group_including_port_variants()
    {
        await using var context = fixture.CreateContext();
        var seed = await IocQueryTestData.InsertIocAsync(
            context, 1, "d-1", "https://evil.example/a", IocType.Url,
            urlHost: "evil.example", parentDomain: "evil.example");
        await IocQueryTestData.InsertIocAsync(
            context, 1, "d-2", "https://evil.example/a", IocType.Url, port: 8443,
            urlHost: "evil.example", parentDomain: "evil.example");
        await IocQueryTestData.InsertIocAsync(
            context, 2, "d-3", "https://evil.example/a", IocType.Url,
            urlHost: "evil.example", parentDomain: "evil.example");

        var reader = CreateReader(fixture.CreateContext());
        var group = await reader.GetDetailGroupAsync(seed.Id, CancellationToken.None);

        Assert.NotNull(group);
        Assert.Equal(seed.Id, group!.Id);
        Assert.Equal("url", group.IocType);
        Assert.Equal("evil.example", group.UrlHost);
        Assert.Equal("evil.example", group.ParentDomain);
        Assert.Equal(3, group.Sources.Count);
    }

    [Fact]
    public async Task Detail_unknown_id_returns_null()
    {
        var reader = CreateReader(fixture.CreateContext());
        Assert.Null(await reader.GetDetailGroupAsync(987654, CancellationToken.None));
    }

    [Fact]
    public async Task Detail_calculates_false_positive_per_source()
    {
        await using var context = fixture.CreateContext();
        var stale = Now.AddDays(-181);
        var seed = await IocQueryTestData.InsertIocAsync(
            context, 1, "fp-1", "10.9.9.9", IocType.Ipv4, updatedAtUtc: stale);

        var reader = CreateReader(fixture.CreateContext());
        var group = await reader.GetDetailGroupAsync(seed.Id, CancellationToken.None);

        var source = Assert.Single(group!.Sources);
        Assert.True(source.IsFalsePositive);
    }

    // --- 1.5 Relations ----------------------------------------------------------

    [Fact]
    public async Task Relations_urlhost_lists_other_groups_and_excludes_seed()
    {
        await using var context = fixture.CreateContext();
        var seed = await IocQueryTestData.InsertIocAsync(
            context, 1, "r-1", "https://evil.example/a", IocType.Url,
            urlHost: "evil.example", parentDomain: "evil.example");
        await IocQueryTestData.InsertIocAsync(
            context, 1, "r-2", "https://evil.example/b", IocType.Url,
            urlHost: "evil.example", parentDomain: "evil.example");
        await IocQueryTestData.InsertIocAsync(
            context, 1, "r-3", "https://other.example/c", IocType.Url,
            urlHost: "other.example", parentDomain: "other.example");

        var reader = CreateReader(fixture.CreateContext());
        var page = await reader.GetRelatedAsync(
            seed.Id, IocRelationKind.UrlHost, 1, 20, CancellationToken.None);

        Assert.NotNull(page);
        Assert.Equal(1, page!.TotalCount);
        var item = Assert.Single(page.Items);
        Assert.Equal("https://evil.example/b", item.IocValue);
    }

    [Fact]
    public async Task Relations_parentdomain_lists_domain_and_url_groups()
    {
        await using var context = fixture.CreateContext();
        var seed = await IocQueryTestData.InsertIocAsync(
            context, 1, "pd-1", "api.example.co.uk", IocType.Domain, parentDomain: "example.co.uk");
        await IocQueryTestData.InsertIocAsync(
            context, 1, "pd-2", "example.co.uk", IocType.Domain, parentDomain: "example.co.uk");
        await IocQueryTestData.InsertIocAsync(
            context, 1, "pd-3", "https://www.example.co.uk/x", IocType.Url,
            urlHost: "www.example.co.uk", parentDomain: "example.co.uk");
        await IocQueryTestData.InsertIocAsync(
            context, 1, "pd-4", "unrelated.example", IocType.Domain, parentDomain: "unrelated.example");

        var reader = CreateReader(fixture.CreateContext());
        var page = await reader.GetRelatedAsync(
            seed.Id, IocRelationKind.ParentDomain, 1, 20, CancellationToken.None);

        Assert.NotNull(page);
        Assert.Equal(2, page!.TotalCount);
        Assert.Contains(page.Items, item => item.IocValue == "example.co.uk");
        Assert.Contains(page.Items, item => item.IocValue == "https://www.example.co.uk/x");
    }

    [Fact]
    public async Task Relations_ip_group_is_rejected()
    {
        await using var context = fixture.CreateContext();
        var seed = await IocQueryTestData.InsertIocAsync(context, 1, "ip-r", "11.11.11.11", IocType.Ipv4);

        var reader = CreateReader(fixture.CreateContext());
        await Assert.ThrowsAsync<IocRelationNotSupportedException>(() =>
            reader.GetRelatedAsync(seed.Id, IocRelationKind.UrlHost, 1, 20, CancellationToken.None));
    }

    [Fact]
    public async Task Relations_unknown_id_returns_null()
    {
        var reader = CreateReader(fixture.CreateContext());
        Assert.Null(await reader.GetRelatedAsync(987654, IocRelationKind.UrlHost, 1, 20, CancellationToken.None));
    }

    [Fact]
    public async Task Relations_without_matches_returns_empty_page()
    {
        await using var context = fixture.CreateContext();
        var seed = await IocQueryTestData.InsertIocAsync(
            context, 1, "lonely", "https://lonely.example/a", IocType.Url,
            urlHost: "lonely.example", parentDomain: "lonely.example");

        var reader = CreateReader(fixture.CreateContext());
        var page = await reader.GetRelatedAsync(
            seed.Id, IocRelationKind.UrlHost, 1, 20, CancellationToken.None);

        Assert.NotNull(page);
        Assert.Empty(page!.Items);
        Assert.Equal(0, page.TotalCount);
    }

    [Fact]
    public async Task Relations_malware_matches_any_source_value_and_excludes_seed()
    {
        await using var context = fixture.CreateContext();
        var seed = await IocQueryTestData.InsertIocAsync(
            context, 1, "rmw-1", "https://evil.example/a", IocType.Url,
            urlHost: "evil.example", parentDomain: "evil.example", malware: "Emotet");
        await IocQueryTestData.InsertIocAsync(
            context, 1, "rmw-2", "https://evil.example/a", IocType.Url, port: 8443,
            urlHost: "evil.example", parentDomain: "evil.example", malware: "TrickBot");
        await IocQueryTestData.InsertIocAsync(
            context, 1, "rmw-3", "https://trick.example/b", IocType.Url,
            urlHost: "trick.example", parentDomain: "trick.example", malware: "trickbot");
        await IocQueryTestData.InsertIocAsync(
            context, 1, "rmw-4", "https://emotet.example/c", IocType.Url,
            urlHost: "emotet.example", parentDomain: "emotet.example", malware: "EMOTET");
        await IocQueryTestData.InsertIocAsync(
            context, 1, "rmw-5", "https://other.example/d", IocType.Url,
            urlHost: "other.example", parentDomain: "other.example", malware: "Qakbot");

        var reader = CreateReader(fixture.CreateContext());
        var page = await reader.GetRelatedAsync(
            seed.Id, IocRelationKind.Malware, 1, 20, CancellationToken.None);

        Assert.NotNull(page);
        Assert.Equal(2, page!.TotalCount);
        Assert.Contains(page.Items, item => item.IocValue == "https://trick.example/b");
        Assert.Contains(page.Items, item => item.IocValue == "https://emotet.example/c");
        Assert.DoesNotContain(page.Items, item => item.IocValue == "https://evil.example/a");
        Assert.DoesNotContain(page.Items, item => item.IocValue == "https://other.example/d");
    }

    [Fact]
    public async Task Relations_target_matches_across_groups()
    {
        await using var context = fixture.CreateContext();
        var seed = await IocQueryTestData.InsertIocAsync(
            context, 1, "rtg-1", "https://amazon.example/a", IocType.Url,
            urlHost: "amazon.example", parentDomain: "amazon.example", target: "Amazon");
        await IocQueryTestData.InsertIocAsync(
            context, 1, "rtg-2", "amazon-other.example", IocType.Domain,
            parentDomain: "amazon-other.example", target: "AMAZON");
        await IocQueryTestData.InsertIocAsync(
            context, 1, "rtg-3", "contoso.example", IocType.Domain,
            parentDomain: "contoso.example", target: "Contoso");

        var reader = CreateReader(fixture.CreateContext());
        var page = await reader.GetRelatedAsync(
            seed.Id, IocRelationKind.Target, 1, 20, CancellationToken.None);

        Assert.NotNull(page);
        Assert.Equal(1, page!.TotalCount);
        Assert.Equal("amazon-other.example", Assert.Single(page.Items).IocValue);
    }

    [Fact]
    public async Task Relations_ip_group_target_is_allowed()
    {
        await using var context = fixture.CreateContext();
        var seed = await IocQueryTestData.InsertIocAsync(
            context, 1, "rip-t-1", "11.22.33.44", IocType.Ipv4, target: "Amazon");
        await IocQueryTestData.InsertIocAsync(
            context, 1, "rip-t-2", "55.66.77.88", IocType.Ipv4, target: "amazon");
        await IocQueryTestData.InsertIocAsync(
            context, 1, "rip-t-3", "99.88.77.66", IocType.Ipv4, target: "Contoso");

        var reader = CreateReader(fixture.CreateContext());
        var page = await reader.GetRelatedAsync(
            seed.Id, IocRelationKind.Target, 1, 20, CancellationToken.None);

        Assert.NotNull(page);
        Assert.Equal("55.66.77.88", Assert.Single(page!.Items).IocValue);
    }

    [Fact]
    public async Task Relations_metadata_without_a_value_returns_empty_page()
    {
        await using var context = fixture.CreateContext();
        var seed = await IocQueryTestData.InsertIocAsync(
            context, 1, "rnm-1", "https://nometa.example/a", IocType.Url,
            urlHost: "nometa.example", parentDomain: "nometa.example");

        var reader = CreateReader(fixture.CreateContext());
        var page = await reader.GetRelatedAsync(
            seed.Id, IocRelationKind.Malware, 1, 20, CancellationToken.None);

        Assert.NotNull(page);
        Assert.Empty(page!.Items);
        Assert.Equal(0, page.TotalCount);
    }

    [Fact]
    public async Task Relations_metadata_unknown_id_returns_null()
    {
        var reader = CreateReader(fixture.CreateContext());
        Assert.Null(await reader.GetRelatedAsync(987654, IocRelationKind.Malware, 1, 20, CancellationToken.None));
    }

    // --- 1.6 Browse -------------------------------------------------------------

    [Fact]
    public async Task Browse_filters_compose_by_type_provider_and_tag()
    {
        await using var context = fixture.CreateContext();
        var url = await IocQueryTestData.InsertIocAsync(
            context, 1, "b-1", "https://phish.example/a", IocType.Url,
            urlHost: "phish.example", parentDomain: "phish.example");
        await IocQueryTestData.TagAsync(context, url, "phishing");
        await IocQueryTestData.InsertIocAsync(
            context, 2, "b-2", "https://phish.example/a", IocType.Url,
            urlHost: "phish.example", parentDomain: "phish.example");
        await IocQueryTestData.InsertIocAsync(
            context, 1, "b-3", "notphish.example", IocType.Domain, parentDomain: "notphish.example");

        var reader = CreateReader(fixture.CreateContext());
        var page = await reader.BrowseAsync(
            new IocBrowseCriteria(IocType.Url, "1", "phishing", null, null, null, null,
                IocBrowseOrderBy.LastSeenUtc, true, 1, 20),
            CancellationToken.None);

        Assert.Equal(1, page.TotalCount);
        var group = Assert.Single(page.Items);
        Assert.Equal("https://phish.example/a", group.IocValue);
        // Tag selection keeps every source of the group, with the tag on the supplying source only.
        Assert.Equal(2, group.Sources.Count);
        Assert.Contains(group.Sources, source => source.Tags.Count == 1);
        Assert.Contains(group.Sources, source => source.Tags.Count == 0);
    }

    [Fact]
    public async Task Browse_filters_by_malware_and_target()
    {
        await using var context = fixture.CreateContext();
        await IocQueryTestData.InsertIocAsync(
            context, 1, "bm-1", "malware.example", IocType.Domain,
            parentDomain: "malware.example", malware: "Emotet");
        await IocQueryTestData.InsertIocAsync(
            context, 1, "bm-2", "target.example", IocType.Domain,
            parentDomain: "target.example", target: "Amazon");
        await IocQueryTestData.InsertIocAsync(
            context, 1, "bm-3", "clean.example", IocType.Domain,
            parentDomain: "clean.example");

        var reader = CreateReader(fixture.CreateContext());

        var byMalware = await reader.BrowseAsync(
            new IocBrowseCriteria(null, null, null, null, null, null, null,
                IocBrowseOrderBy.IocValue, false, 1, 20, Malware: "emotet"),
            CancellationToken.None);
        Assert.Equal("malware.example", Assert.Single(byMalware.Items).IocValue);

        var byTarget = await reader.BrowseAsync(
            new IocBrowseCriteria(null, null, null, null, null, null, null,
                IocBrowseOrderBy.IocValue, false, 1, 20, Target: " AMAZON "),
            CancellationToken.None);
        Assert.Equal("target.example", Assert.Single(byTarget.Items).IocValue);
    }

    [Fact]
    public async Task Browse_metadata_filters_compose_with_type_and_provider()
    {
        await using var context = fixture.CreateContext();
        await IocQueryTestData.InsertIocAsync(
            context, 1, "bc-1", "https://emotet.example/a", IocType.Url,
            urlHost: "emotet.example", parentDomain: "emotet.example", malware: "Emotet");
        await IocQueryTestData.InsertIocAsync(
            context, 2, "bc-2", "https://emotet.example/b", IocType.Url,
            urlHost: "emotet.example", parentDomain: "emotet.example", malware: "Emotet");
        await IocQueryTestData.InsertIocAsync(
            context, 1, "bc-3", "emotet.example", IocType.Domain,
            parentDomain: "emotet.example", malware: "Emotet");

        var reader = CreateReader(fixture.CreateContext());
        var page = await reader.BrowseAsync(
            new IocBrowseCriteria(IocType.Url, "1", null, null, null, null, null,
                IocBrowseOrderBy.IocValue, false, 1, 20, Malware: "emotet"),
            CancellationToken.None);

        Assert.Equal(1, page.TotalCount);
        var group = Assert.Single(page.Items);
        Assert.Equal("https://emotet.example/a", group.IocValue);
        Assert.Single(group.Sources);
    }

    [Fact]
    public async Task Browse_metadata_filter_without_matches_returns_empty_envelope()
    {
        await using var context = fixture.CreateContext();
        await IocQueryTestData.InsertIocAsync(
            context, 1, "be-1", "be.example", IocType.Domain,
            parentDomain: "be.example", malware: "Emotet");

        var reader = CreateReader(fixture.CreateContext());
        var page = await reader.BrowseAsync(
            new IocBrowseCriteria(null, null, null, null, null, null, null,
                IocBrowseOrderBy.LastSeenUtc, true, 1, 20, Target: "does-not-exist"),
            CancellationToken.None);

        Assert.Empty(page.Items);
        Assert.Equal(0, page.TotalCount);
    }

    [Fact]
    public async Task Browse_multiple_tags_match_when_any_selected_tag_is_present()
    {
        await using var context = fixture.CreateContext();
        var phishing = await IocQueryTestData.InsertIocAsync(
            context, 1, "b-or-1", "https://phish-only.example/a", IocType.Url,
            urlHost: "phish-only.example", parentDomain: "phish-only.example");
        await IocQueryTestData.TagAsync(context, phishing, "phishing");

        var botnet = await IocQueryTestData.InsertIocAsync(
            context, 1, "b-or-2", "botnet-only.example", IocType.Domain,
            parentDomain: "botnet-only.example");
        await IocQueryTestData.TagAsync(context, botnet, "botnet");

        var unrelated = await IocQueryTestData.InsertIocAsync(
            context, 1, "b-or-3", "clean.example", IocType.Domain,
            parentDomain: "clean.example");
        await IocQueryTestData.TagAsync(context, unrelated, "malware");

        var reader = CreateReader(fixture.CreateContext());
        var page = await reader.BrowseAsync(
            new IocBrowseCriteria(null, "1", "phishing,botnet", null, null, null, null,
                IocBrowseOrderBy.IocValue, false, 1, 20),
            CancellationToken.None);

        Assert.Equal(2, page.TotalCount);
        Assert.Equal(
            ["botnet-only.example", "https://phish-only.example/a"],
            page.Items.Select(item => item.IocValue).ToArray());
    }

    [Fact]
    public async Task Browse_default_order_is_last_seen_desc_with_nulls_last()
    {
        await using var context = fixture.CreateContext();
        await IocQueryTestData.InsertIocAsync(
            context, 1, "o-1", "old.example", IocType.Domain, lastSeenUtc: Now.AddDays(-5), parentDomain: "old.example");
        await IocQueryTestData.InsertIocAsync(
            context, 1, "o-2", "new.example", IocType.Domain, lastSeenUtc: Now.AddDays(-1), parentDomain: "new.example");
        await IocQueryTestData.InsertIocAsync(
            context, 1, "o-3", "never.example", IocType.Domain, parentDomain: "never.example");

        var reader = CreateReader(fixture.CreateContext());
        var page = await reader.BrowseAsync(
            new IocBrowseCriteria(null, null, null, null, null, null, null,
                IocBrowseOrderBy.LastSeenUtc, true, 1, 20),
            CancellationToken.None);

        Assert.Equal(["new.example", "old.example", "never.example"], page.Items.Select(i => i.IocValue));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Browse_null_aggregate_sorts_last_in_both_directions(bool descending)
    {
        await using var context = fixture.CreateContext();
        await IocQueryTestData.InsertIocAsync(
            context, 1, "n-1", "dated.example", IocType.Domain, lastSeenUtc: Now.AddDays(-2), parentDomain: "dated.example");
        await IocQueryTestData.InsertIocAsync(
            context, 1, "n-2", "nullexample.example", IocType.Domain, parentDomain: "nullexample.example");

        var reader = CreateReader(fixture.CreateContext());
        var page = await reader.BrowseAsync(
            new IocBrowseCriteria(null, null, null, null, null, null, null,
                IocBrowseOrderBy.LastSeenUtc, descending, 1, 20),
            CancellationToken.None);

        Assert.Equal("nullexample.example", page.Items[^1].IocValue);
    }

    [Fact]
    public async Task Browse_orders_equal_aggregates_by_type_then_value()
    {
        await using var context = fixture.CreateContext();
        await IocQueryTestData.InsertIocAsync(
            context, 1, "tie-1", "b.example", IocType.Domain, lastSeenUtc: Now.AddDays(-1), parentDomain: "b.example");
        await IocQueryTestData.InsertIocAsync(
            context, 1, "tie-2", "a.example", IocType.Domain, lastSeenUtc: Now.AddDays(-1), parentDomain: "a.example");
        await IocQueryTestData.InsertIocAsync(
            context, 1, "tie-3", "1.1.2.2", IocType.Ipv4, lastSeenUtc: Now.AddDays(-1));

        var reader = CreateReader(fixture.CreateContext());
        var page = await reader.BrowseAsync(
            new IocBrowseCriteria(null, null, null, null, null, null, null,
                IocBrowseOrderBy.LastSeenUtc, true, 1, 20),
            CancellationToken.None);

        // Ipv4 (type ordinal 0) precedes domains, then value ordinal within the type.
        Assert.Equal(["1.1.2.2", "a.example", "b.example"], page.Items.Select(i => i.IocValue));
    }

    [Fact]
    public async Task Browse_pages_are_stable_and_total_is_page_independent()
    {
        await using var context = fixture.CreateContext();
        for (var index = 0; index < 5; index++)
        {
            await IocQueryTestData.InsertIocAsync(
                context, 1, $"pg-{index}", $"page{index}.example", IocType.Domain,
                lastSeenUtc: Now.AddDays(-index), parentDomain: $"page{index}.example");
        }

        var reader = CreateReader(fixture.CreateContext());
        var criteria = new IocBrowseCriteria(null, null, null, null, null, null, null,
            IocBrowseOrderBy.IocValue, false, 1, 2);
        var first = await reader.BrowseAsync(criteria, CancellationToken.None);
        var second = await reader.BrowseAsync(criteria with { Page = 2 }, CancellationToken.None);

        Assert.Equal(5, first.TotalCount);
        Assert.Equal(5, second.TotalCount);
        Assert.Equal(["page0.example", "page1.example"], first.Items.Select(i => i.IocValue));
        Assert.Equal(["page2.example", "page3.example"], second.Items.Select(i => i.IocValue));
    }

    [Fact]
    public async Task Browse_datetime_range_bounds_the_aggregate()
    {
        await using var context = fixture.CreateContext();
        await IocQueryTestData.InsertIocAsync(
            context, 1, "dr-1", "in.example", IocType.Domain, lastSeenUtc: Now.AddDays(-2), parentDomain: "in.example");
        await IocQueryTestData.InsertIocAsync(
            context, 1, "dr-2", "out.example", IocType.Domain, lastSeenUtc: Now.AddDays(-30), parentDomain: "out.example");

        var reader = CreateReader(fixture.CreateContext());
        var page = await reader.BrowseAsync(
            new IocBrowseCriteria(null, null, null, null, null, Now.AddDays(-5), null,
                IocBrowseOrderBy.LastSeenUtc, true, 1, 20),
            CancellationToken.None);

        var group = Assert.Single(page.Items);
        Assert.Equal("in.example", group.IocValue);
    }

    // --- 2.x Batch search -------------------------------------------------------

    [Fact]
    public async Task Batch_matches_the_single_path_for_the_same_identity()
    {
        await using var context = fixture.CreateContext();
        await IocQueryTestData.InsertIocAsync(context, 1, "bs-1", "1.2.3.4", IocType.Ipv4, lastSeenUtc: Recent);
        await IocQueryTestData.InsertIocAsync(context, 2, "bs-2", "1.2.3.4", IocType.Ipv4, lastSeenUtc: Recent);

        var identity = new NormalizationResult(IocType.Ipv4, "1.2.3.4", null, null, null);
        var reader = CreateReader(fixture.CreateContext());
        var single = await reader.SearchGroupsAsync(identity, new IocSourceFilter(), 1, 20, CancellationToken.None);
        var batch = await reader.SearchBatchAsync([identity], new IocSourceFilter(), 1, 20, CancellationToken.None);

        var page = batch[identity];
        Assert.Equal(single.TotalCount, page.TotalCount);
        var singleGroup = Assert.Single(single.Items);
        var batchGroup = Assert.Single(page.Items);
        Assert.Equal(singleGroup.Id, batchGroup.Id);
        Assert.Equal(
            singleGroup.Sources.Select(source => (source.Provider, source.ProviderRecordId)),
            batchGroup.Sources.Select(source => (source.Provider, source.ProviderRecordId)));
    }

    [Fact]
    public async Task Batch_domain_returns_exact_url_host_and_parent_domain_groups()
    {
        await using var context = fixture.CreateContext();
        await IocQueryTestData.InsertIocAsync(
            context, 1, "bd-1", "https://shop.example.co.uk/a", IocType.Url,
            urlHost: "shop.example.co.uk", parentDomain: "example.co.uk");
        await IocQueryTestData.InsertIocAsync(
            context, 1, "bd-2", "api.example.co.uk", IocType.Domain,
            parentDomain: "example.co.uk");

        var identity = new NormalizationResult(IocType.Domain, "example.co.uk", null, null, "example.co.uk");
        var reader = CreateReader(fixture.CreateContext());
        var batch = await reader.SearchBatchAsync([identity], new IocSourceFilter(), 1, 20, CancellationToken.None);

        var page = batch[identity];
        Assert.Equal(2, page.TotalCount);
        Assert.Contains(page.Items, item => item.IocValue == "api.example.co.uk");
        Assert.Contains(page.Items, item => item.IocValue == "https://shop.example.co.uk/a");
    }

    [Fact]
    public async Task Batch_port_variant_keeps_every_attributed_source_without_row_loss()
    {
        await using var context = fixture.CreateContext();
        await IocQueryTestData.InsertIocAsync(
            context, 1, "bp-1", "https://evil.example/a", IocType.Url, port: 8443,
            urlHost: "evil.example", parentDomain: "evil.example");
        await IocQueryTestData.InsertIocAsync(
            context, 2, "bp-2", "https://evil.example/a", IocType.Url, port: 8443,
            urlHost: "evil.example", parentDomain: "evil.example");

        var identity = new NormalizationResult(IocType.Url, "https://evil.example/a", 8443, "evil.example", "evil.example");
        var reader = CreateReader(fixture.CreateContext());
        var batch = await reader.SearchBatchAsync([identity], new IocSourceFilter(), 1, 20, CancellationToken.None);

        var group = Assert.Single(batch[identity].Items);
        Assert.Equal(2, group.Sources.Count);
        Assert.All(group.Sources, source => Assert.Equal(8443, source.Port));
        Assert.Contains(group.Sources, source => source.Provider == "ThreatFox");
        Assert.Contains(group.Sources, source => source.Provider == "PhishTank");
    }

    [Fact]
    public async Task Batch_applies_filters_independently_to_every_identity()
    {
        await using var context = fixture.CreateContext();
        await IocQueryTestData.InsertIocAsync(context, 1, "bf-1", "1.1.1.1", IocType.Ipv4);
        await IocQueryTestData.InsertIocAsync(context, 2, "bf-2", "1.1.1.1", IocType.Ipv4);
        await IocQueryTestData.InsertIocAsync(context, 1, "bf-3", "2.2.2.2", IocType.Ipv4);

        var first = new NormalizationResult(IocType.Ipv4, "1.1.1.1", null, null, null);
        var second = new NormalizationResult(IocType.Ipv4, "2.2.2.2", null, null, null);
        var absent = new NormalizationResult(IocType.Ipv4, "9.9.9.9", null, null, null);
        var reader = CreateReader(fixture.CreateContext());
        var batch = await reader.SearchBatchAsync(
            [first, second, absent],
            new IocSourceFilter(Provider: "1"),
            1,
            20,
            CancellationToken.None);

        var firstGroup = Assert.Single(batch[first].Items);
        Assert.Equal("ThreatFox", Assert.Single(firstGroup.Sources).Provider);
        Assert.Single(batch[second].Items);
        Assert.Equal(0, batch[absent].TotalCount);
    }

    [Fact]
    public async Task Batch_pages_each_identity_independently()
    {
        await using var context = fixture.CreateContext();
        for (var index = 0; index < 3; index++)
        {
            await IocQueryTestData.InsertIocAsync(
                context, 1, $"bpg-{index}", $"https://page{index}.example/a", IocType.Url,
                urlHost: "page.example", parentDomain: "page.example", lastSeenUtc: Now.AddDays(-index));
        }

        var identity = new NormalizationResult(IocType.Domain, "page.example", null, null, "page.example");
        var reader = CreateReader(fixture.CreateContext());
        var firstPage = await reader.SearchBatchAsync([identity], new IocSourceFilter(), 1, 2, CancellationToken.None);
        var secondPage = await reader.SearchBatchAsync([identity], new IocSourceFilter(), 2, 2, CancellationToken.None);

        Assert.Equal(3, firstPage[identity].TotalCount);
        Assert.Equal(2, firstPage[identity].Items.Count);
        Assert.Single(secondPage[identity].Items);
    }

    // --- 2.4 Suggestions --------------------------------------------------------

    [Fact]
    public async Task Suggestions_collapse_provider_duplicates_and_order_by_value()
    {
        await using var context = fixture.CreateContext();
        await IocQueryTestData.InsertIocAsync(context, 1, "sg-1", "evil.example", IocType.Domain, parentDomain: "evil.example");
        await IocQueryTestData.InsertIocAsync(context, 2, "sg-2", "evil.example", IocType.Domain, parentDomain: "evil.example");
        await IocQueryTestData.InsertIocAsync(context, 1, "sg-3", "evilother.example", IocType.Domain, parentDomain: "evilother.example");

        var reader = CreateReader(fixture.CreateContext());
        var suggestions = await reader.GetIocSuggestionsAsync("EVIL", 8, CancellationToken.None);

        Assert.Equal(
            ["evil.example", "evilother.example"],
            suggestions.Select(item => item.Value).ToArray());
        Assert.All(suggestions, item => Assert.Equal("domain", item.IocType));
    }

    [Fact]
    public async Task Suggestions_respect_the_limit_and_return_empty_when_nothing_matches()
    {
        await using var context = fixture.CreateContext();
        for (var index = 0; index < 5; index++)
        {
            await IocQueryTestData.InsertIocAsync(
                context, 1, $"sl-{index}", $"alpha{index}.example", IocType.Domain,
                parentDomain: $"alpha{index}.example");
        }

        var reader = CreateReader(fixture.CreateContext());
        var limited = await reader.GetIocSuggestionsAsync("alpha", 3, CancellationToken.None);
        Assert.Equal(3, limited.Count);
        Assert.Equal("alpha0.example", limited[0].Value);

        var none = await reader.GetIocSuggestionsAsync("zeta", 8, CancellationToken.None);
        Assert.Empty(none);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
