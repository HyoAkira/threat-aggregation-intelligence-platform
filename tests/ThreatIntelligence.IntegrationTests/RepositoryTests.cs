using Microsoft.EntityFrameworkCore;
using ThreatIntelligence.Domain.Entities;
using ThreatIntelligence.Domain.Enums;
using ThreatIntelligence.Infrastructure.Hashing;
using ThreatIntelligence.Infrastructure.Persistence;
using ThreatIntelligence.Infrastructure.Persistence.Repositories;

namespace ThreatIntelligence.IntegrationTests;

[Collection("sqlserver")]
public class RepositoryTests(SqlServerFixture fixture)
{
    private readonly Sha256ValueHasher _hasher = new();

    [Fact]
    public async Task Endpoint_lookup_finds_null_port_candidate()
    {
        await using var context = fixture.CreateContext();
        await TestData.InsertIocRawAsync(context, 1, "e-1", "10.0.0.1", _hasher.Hash("10.0.0.1"), (byte)IocType.Ipv4);

        var repo = new IocRepository(fixture.CreateContext(), _hasher);
        var found = await repo.FindByEndpointIdentityAsync(1, IocType.Ipv4, "10.0.0.1", null, CancellationToken.None);
        Assert.NotNull(found);
        Assert.Equal("e-1", found!.ProviderRecordId);
    }

    [Fact]
    public async Task Endpoint_lookup_finds_equal_port_candidate()
    {
        await using var context = fixture.CreateContext();
        await TestData.InsertIocRawAsync(context, 1, "e-2", "10.0.0.2", _hasher.Hash("10.0.0.2"), (byte)IocType.Ipv4, port: 443);

        var repo = new IocRepository(fixture.CreateContext(), _hasher);
        var found = await repo.FindByEndpointIdentityAsync(1, IocType.Ipv4, "10.0.0.2", 443, CancellationToken.None);
        Assert.NotNull(found);
    }

    [Fact]
    public async Task Endpoint_lookup_does_not_match_different_port_candidate()
    {
        await using var context = fixture.CreateContext();
        await TestData.InsertIocRawAsync(context, 1, "e-3", "10.0.0.3", _hasher.Hash("10.0.0.3"), (byte)IocType.Ipv4, port: 80);

        var repo = new IocRepository(fixture.CreateContext(), _hasher);
        var found = await repo.FindByEndpointIdentityAsync(1, IocType.Ipv4, "10.0.0.3", 443, CancellationToken.None);
        Assert.Null(found);
    }

    [Fact]
    public async Task Provider_record_id_lookup_finds_the_row()
    {
        await using var context = fixture.CreateContext();
        await TestData.InsertIocRawAsync(context, 1, "native-1", "10.0.0.4", _hasher.Hash("10.0.0.4"), (byte)IocType.Ipv4);

        var repo = new IocRepository(fixture.CreateContext(), _hasher);
        var found = await repo.FindByProviderRecordIdAsync(1, "native-1", CancellationToken.None);
        Assert.NotNull(found);
    }

    [Fact]
    public async Task Provider_record_id_lookup_returns_null_when_absent()
    {
        var repo = new IocRepository(fixture.CreateContext(), _hasher);
        var found = await repo.FindByProviderRecordIdAsync(1, "missing", CancellationToken.None);
        Assert.Null(found);
    }

    [Fact]
    public async Task Grouped_read_returns_all_provider_and_port_source_rows()
    {
        await using var context = fixture.CreateContext();
        var hash = _hasher.Hash("shared.example");
        await TestData.InsertIocRawAsync(context, 1, "g-1", "shared.example", hash, (byte)IocType.Domain);
        await TestData.InsertIocRawAsync(context, 2, "g-2", "shared.example", hash, (byte)IocType.Domain, port: 443);

        var repo = new IocRepository(fixture.CreateContext(), _hasher);
        var rows = await repo.GetGroupedAsync(IocType.Domain, "shared.example", CancellationToken.None);

        Assert.Equal(2, rows.Count);
        Assert.Equal(new[] { 1, 2 }, rows.Select(r => r.ProviderId).OrderBy(p => p).ToArray());
        Assert.Contains(rows, r => r.Port == 443);
        Assert.Contains(rows, r => r.Port == null);
    }

    [Fact]
    public async Task Grouped_read_excludes_a_simulated_hash_collision()
    {
        await using var context = fixture.CreateContext();
        var hashOfOther = _hasher.Hash("different.example");
        await TestData.InsertIocRawAsync(context, 1, "g-col", "shared.example", hashOfOther, (byte)IocType.Domain);

        var repo = new IocRepository(fixture.CreateContext(), _hasher);
        var rows = await repo.GetGroupedAsync(IocType.Domain, "different.example", CancellationToken.None);

        Assert.Empty(rows);
    }

    [Fact]
    public async Task Fetch_history_can_be_created_and_updated_independently()
    {
        var repo = new FetchHistoryRepository(fixture.CreateContextFactory());
        var jobId = Guid.NewGuid();

        await repo.AddAsync(new FetchHistory(
            jobId, 1, FetchTrigger.Scheduled, FetchMode.Recent, FetchStatus.Running, TestData.Utc),
            CancellationToken.None);

        var running = await repo.GetByJobIdAsync(jobId, CancellationToken.None);
        Assert.NotNull(running);
        Assert.Equal(FetchStatus.Running, running!.Status);

        running.Succeed(12, 3, TestData.Utc.AddMinutes(5));
        await repo.UpdateAsync(running, CancellationToken.None);

        var succeeded = await repo.GetByJobIdAsync(jobId, CancellationToken.None);
        Assert.NotNull(succeeded);
        Assert.Equal(FetchStatus.Success, succeeded!.Status);
        Assert.Equal(12, succeeded.RecordsAdded);
        Assert.Equal(3, succeeded.RecordsUpdated);
        Assert.Equal(TestData.Utc.AddMinutes(5), succeeded.FinishedAt);
    }
}