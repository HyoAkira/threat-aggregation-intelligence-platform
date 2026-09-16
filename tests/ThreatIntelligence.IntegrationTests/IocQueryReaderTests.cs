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

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
