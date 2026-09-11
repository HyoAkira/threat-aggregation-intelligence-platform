using Microsoft.EntityFrameworkCore;
using ThreatIntelligence.Domain.Entities;
using ThreatIntelligence.Domain.Enums;
using ThreatIntelligence.Infrastructure.Hashing;
using ThreatIntelligence.Infrastructure.Persistence;
using ThreatIntelligence.Infrastructure.Persistence.Repositories;

namespace ThreatIntelligence.IntegrationTests;

[Collection("sqlserver")]
public class UnitOfWorkTests(SqlServerFixture fixture)
{
    private static readonly DateTime Utc = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Sha256ValueHasher Hasher = new();

    private static IoC NewIoc(string recordId, string value, int? port = null) => new(
        providerId: 1,
        providerRecordId: recordId,
        iocValue: value,
        iocValueHash: Hasher.Hash(value),
        iocType: IocType.Ipv4,
        firstSeenUtc: Utc,
        createdAt: Utc,
        updatedAt: Utc,
        port: port);

    [Fact]
    public async Task Commit_persists_all_staged_ioc_and_tag_changes()
    {
        await using var context = fixture.CreateContext();
        var unitOfWork = new UnitOfWork(context);

        await unitOfWork.BeginAsync(CancellationToken.None);

        var ioc = NewIoc("uow-1", "10.1.0.1");
        context.Iocs.Add(ioc);
        var tag = new Tag("botnet");
        context.Tags.Add(tag);
        await context.SaveChangesAsync();
        context.IoCTags.Add(new IoCTag(ioc.Id, tag.Id));

        await unitOfWork.CommitAsync(CancellationToken.None);

        await using var verify = fixture.CreateContext();
        Assert.Equal(1, await verify.Iocs.CountAsync(i => i.ProviderRecordId == "uow-1"));
        Assert.Equal(1, await verify.Tags.CountAsync(t => t.Name == "botnet"));
        Assert.Equal(1, await verify.IoCTags.CountAsync());
    }

    [Fact]
    public async Task Rollback_discards_all_staged_changes()
    {
        await using var context = fixture.CreateContext();
        var unitOfWork = new UnitOfWork(context);
        var countBefore = await context.Iocs.CountAsync();

        await unitOfWork.BeginAsync(CancellationToken.None);
        context.Iocs.Add(NewIoc("uow-2", "10.1.0.2"));
        await context.SaveChangesAsync();
        await unitOfWork.RollbackAsync(CancellationToken.None);

        Assert.Equal(countBefore, await fixture.CreateContext().Iocs.CountAsync());
    }

    [Fact]
    public async Task Rolled_back_batch_keeps_a_separate_failed_history_row()
    {
        await using var context = fixture.CreateContext();
        var unitOfWork = new UnitOfWork(context);
        var jobId = Guid.NewGuid();
        var historyRepo = new FetchHistoryRepository(fixture.CreateContextFactory());

        await unitOfWork.BeginAsync(CancellationToken.None);
        context.Iocs.Add(NewIoc("uow-3", "10.1.0.3"));
        await context.SaveChangesAsync();

        await historyRepo.AddAsync(new FetchHistory(
            jobId, 1, FetchTrigger.Scheduled, FetchMode.Recent, FetchStatus.Failed,
            Utc, finishedAt: Utc.AddMinutes(1), errorDetail: "Invalid record 'bad' at index 3"),
            CancellationToken.None);

        await unitOfWork.RollbackAsync(CancellationToken.None);

        Assert.Equal(0, await fixture.CreateContext().Iocs.CountAsync(i => i.ProviderRecordId == "uow-3"));

        var failed = await historyRepo.GetByJobIdAsync(jobId, CancellationToken.None);
        Assert.NotNull(failed);
        Assert.Equal(FetchStatus.Failed, failed!.Status);
        Assert.Equal(0, failed.RecordsAdded);
        Assert.Equal(0, failed.RecordsUpdated);
        Assert.Equal("Invalid record 'bad' at index 3", failed.ErrorDetail);
    }

    [Fact]
    public async Task Successful_batch_records_committed_counters()
    {
        await using var context = fixture.CreateContext();
        var unitOfWork = new UnitOfWork(context);
        var jobId = Guid.NewGuid();
        var historyRepo = new FetchHistoryRepository(fixture.CreateContextFactory());

        await unitOfWork.BeginAsync(CancellationToken.None);
        context.Iocs.Add(NewIoc("uow-4", "10.1.0.4"));
        await context.SaveChangesAsync();
        await unitOfWork.CommitAsync(CancellationToken.None);

        await historyRepo.AddAsync(new FetchHistory(
            jobId, 1, FetchTrigger.Scheduled, FetchMode.Recent, FetchStatus.Success,
            Utc, finishedAt: Utc.AddMinutes(1), recordsAdded: 1, recordsUpdated: 0),
            CancellationToken.None);

        var history = await historyRepo.GetByJobIdAsync(jobId, CancellationToken.None);
        Assert.NotNull(history);
        Assert.Equal(FetchStatus.Success, history!.Status);
        Assert.Equal(1, history.RecordsAdded);
        Assert.Equal(1, await fixture.CreateContext().Iocs.CountAsync(i => i.ProviderRecordId == "uow-4"));
    }
}