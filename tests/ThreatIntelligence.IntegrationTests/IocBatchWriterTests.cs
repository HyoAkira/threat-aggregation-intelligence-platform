using Microsoft.EntityFrameworkCore;
using ThreatIntelligence.Application.BatchWriter;
using ThreatIntelligence.Application.Normalization;
using ThreatIntelligence.Application.ProviderRecords;
using ThreatIntelligence.Domain.Entities;
using ThreatIntelligence.Domain.Enums;
using ThreatIntelligence.Infrastructure.Hashing;
using ThreatIntelligence.Infrastructure.PhishTank;
using ThreatIntelligence.Infrastructure.Pipeline;
using ThreatIntelligence.Infrastructure.Persistence;
using ThreatIntelligence.Infrastructure.Persistence.Repositories;
using ThreatIntelligence.Infrastructure.PublicSuffix;

namespace ThreatIntelligence.IntegrationTests;

[Collection("sqlserver")]
public sealed class IocBatchWriterTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private static readonly DateTime FirstSeen = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime LastSeen = new(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Sha256ValueHasher Hasher = new();

    private readonly ProviderRecordValidator _validator = CreateValidator();

    public Task InitializeAsync() => fixture.ResetDataAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private IocBatchWriter CreateWriter()
    {
        var mapper = new PhishTankUpsertFieldsMapper(Hasher, TimeProvider.System);
        var mappers = new Dictionary<int, IIocUpsertFieldsMapper>
        {
            [PhishTankProviderValidator.PhishTankProviderId] = mapper
        };
        return new IocBatchWriter(fixture.CreateContext(), mappers);
    }

    private NormalizedProviderRecord Rec(
        string recordId,
        string value,
        DateTime? firstSeenUtc = null,
        DateTime? lastSeenUtc = null,
        bool? verified = null,
        string? target = null,
        IReadOnlyCollection<string?>? tags = null)
    {
        var input = new ProviderRecordInput(
            ProviderId: PhishTankProviderValidator.PhishTankProviderId,
            ProviderRecordId: recordId,
            RawIoc: value,
            DeclaredType: null,
            FirstSeenAt: firstSeenUtc ?? FirstSeen,
            LastSeenAt: lastSeenUtc ?? LastSeen,
            ProviderDetailsLink: null,
            Tags: tags,
            Metadata: verified is null && target is null
                ? null
                : new Dictionary<string, object?>
                {
                    [PhishTankRecordMapper.VerifiedKey] = verified,
                    [PhishTankRecordMapper.TargetKey] = target
                },
            Payload: null);

        var result = _validator.Validate(input, recordIndex: 1);
        Assert.True(result.IsValid, $"Record {recordId} should validate: {result.Failure?.Message}");
        return result.Normalized!;
    }

    private static ProviderRecordValidator CreateValidator()
    {
        var resolver = new PublicSuffixParentDomainResolver();
        var classifier = new IocClassifier(
            new Ipv4Normalizer(),
            new Ipv6Normalizer(),
            new UrlNormalizer(resolver),
            new DomainNormalizer(resolver));
        return new ProviderRecordValidator(
            classifier,
            new Dictionary<int, IProviderValidator>
            {
                [PhishTankProviderValidator.PhishTankProviderId] = new PhishTankProviderValidator()
            });
    }

    // --- TI-080 Identity resolution ------------------------------------------------

    [Fact]
    public async Task New_endpoint_identity_inserts_and_counts_added()
    {
        var writer = CreateWriter();
        var result = await writer.ApplyAsync(
            new[] { Rec("r1", "1.2.3.4"), Rec("r2", "10.0.0.2") },
            PhishTankProviderValidator.PhishTankProviderId,
            CancellationToken.None);

        Assert.Equal(2, result.Added);
        Assert.Equal(0, result.Updated);

        await using var context = fixture.CreateContext();
        Assert.Equal(2, await context.Iocs.CountAsync(i => i.ProviderId == 2));
    }

    [Fact]
    public async Task Existing_endpoint_identity_updates_stored_row_without_duplicating()
    {
        var writer = CreateWriter();
        var first = await writer.ApplyAsync(
            new[] { Rec("r1", "1.2.3.4") },
            PhishTankProviderValidator.PhishTankProviderId,
            CancellationToken.None);
        var second = await writer.ApplyAsync(
            new[] { Rec("r1", "1.2.3.4") },
            PhishTankProviderValidator.PhishTankProviderId,
            CancellationToken.None);

        Assert.Equal((1, 0), (first.Added, first.Updated));
        Assert.Equal((0, 1), (second.Added, second.Updated));

        await using var context = fixture.CreateContext();
        Assert.Equal(1, await context.Iocs.CountAsync(i => i.ProviderId == 2));
    }

    [Fact]
    public async Task Provider_record_id_is_authoritative_when_value_changes()
    {
        var writer = CreateWriter();
        await writer.ApplyAsync(
            new[] { Rec("r1", "1.2.3.4") },
            PhishTankProviderValidator.PhishTankProviderId,
            CancellationToken.None);

        var result = await writer.ApplyAsync(
            new[] { Rec("r1", "1.2.3.5") },
            PhishTankProviderValidator.PhishTankProviderId,
            CancellationToken.None);

        Assert.Equal((0, 1), (result.Added, result.Updated));

        await using var context = fixture.CreateContext();
        var rows = await context.Iocs.Where(i => i.ProviderId == 2).ToListAsync();
        var row = Assert.Single(rows);
        Assert.Equal("r1", row.ProviderRecordId);
        Assert.Equal("1.2.3.5", row.IocValue);
    }

    [Fact]
    public async Task Endpoint_identity_handles_record_that_changed_id()
    {
        var writer = CreateWriter();
        await writer.ApplyAsync(
            new[] { Rec("old-id", "1.2.3.4") },
            PhishTankProviderValidator.PhishTankProviderId,
            CancellationToken.None);

        var result = await writer.ApplyAsync(
            new[] { Rec("new-id", "1.2.3.4") },
            PhishTankProviderValidator.PhishTankProviderId,
            CancellationToken.None);

        Assert.Equal((0, 1), (result.Added, result.Updated));

        await using var context = fixture.CreateContext();
        var row = await context.Iocs.SingleAsync(i => i.ProviderId == 2);
        Assert.Equal("new-id", row.ProviderRecordId);
        Assert.Equal("1.2.3.4", row.IocValue);
    }

    [Fact]
    public async Task Identity_conflict_rejects_the_entire_fetch()
    {
        await using (var seed = fixture.CreateContext())
        {
            seed.Iocs.AddRange(
                RecIoc("x", "1.2.3.4"),
                RecIoc("y", "10.0.0.9"));
            await seed.SaveChangesAsync();
        }

        var writer = CreateWriter();

        var ex = await Assert.ThrowsAsync<IocIdentityConflictException>(() =>
            writer.ApplyAsync(
                new[] { Rec("x", "10.0.0.9") },
                PhishTankProviderValidator.PhishTankProviderId,
                CancellationToken.None));

        Assert.Contains("x", ex.Message);

        await using var context = fixture.CreateContext();
        Assert.Equal(2, await context.Iocs.CountAsync(i => i.ProviderId == 2));
    }

    // --- TI-081 / FR-006 Upsert field policy --------------------------------------

    [Fact]
    public async Task Upsert_preserves_creation_and_first_seen()
    {
        var writer = CreateWriter();
        var original = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        await writer.ApplyAsync(
            new[] { Rec("r1", "1.2.3.4", firstSeenUtc: original) },
            PhishTankProviderValidator.PhishTankProviderId,
            CancellationToken.None);

        await writer.ApplyAsync(
            new[] { Rec("r1", "1.2.3.4", firstSeenUtc: original.AddDays(30)) },
            PhishTankProviderValidator.PhishTankProviderId,
            CancellationToken.None);

        await using var context = fixture.CreateContext();
        var row = await context.Iocs.SingleAsync(i => i.ProviderId == 2);
        Assert.Equal(original, row.FirstSeenUtc);
    }

    [Fact]
    public async Task Upsert_refreshes_updated_at_last_seen_and_metadata()
    {
        var writer = CreateWriter();
        await writer.ApplyAsync(
            new[] { Rec("r1", "1.2.3.4", lastSeenUtc: LastSeen, verified: true, target: "Old Bank") },
            PhishTankProviderValidator.PhishTankProviderId,
            CancellationToken.None);

        var newLastSeen = LastSeen.AddHours(5);
        await writer.ApplyAsync(
            new[] { Rec("r1", "1.2.3.4", lastSeenUtc: newLastSeen, verified: false, target: "New Bank") },
            PhishTankProviderValidator.PhishTankProviderId,
            CancellationToken.None);

        await using var context = fixture.CreateContext();
        var row = await context.Iocs.SingleAsync(i => i.ProviderId == 2);
        Assert.Equal(newLastSeen, row.LastSeenUtc);
        Assert.False(row.IsVerified);
        Assert.Equal("New Bank", row.Target);
        Assert.True(row.UpdatedAt > row.CreatedAt);
    }

    [Fact]
    public async Task Identical_refetch_counts_as_updated_not_added()
    {
        var writer = CreateWriter();
        var first = await writer.ApplyAsync(
            new[] { Rec("r1", "1.2.3.4") },
            PhishTankProviderValidator.PhishTankProviderId,
            CancellationToken.None);
        var second = await writer.ApplyAsync(
            new[] { Rec("r1", "1.2.3.4") },
            PhishTankProviderValidator.PhishTankProviderId,
            CancellationToken.None);

        Assert.Equal(1, first.Added);
        Assert.Equal(0, first.Updated);
        Assert.Equal(0, second.Added);
        Assert.Equal(1, second.Updated);
        Assert.Equal(1, second.Added + second.Updated);
    }

    // --- TI-082 Tag replacement ----------------------------------------------------

    [Fact]
    public async Task Tags_are_normalized_and_deduplicated()
    {
        var writer = CreateWriter();
        await writer.ApplyAsync(
            new[] { Rec("r1", "1.2.3.4", tags: new[] { " BotNet ", "botnet", "BOTNET" }) },
            PhishTankProviderValidator.PhishTankProviderId,
            CancellationToken.None);

        await using var context = fixture.CreateContext();
        var tag = await context.Tags.SingleAsync(t => t.Name == "botnet");
        var row = await context.Iocs.SingleAsync(i => i.ProviderId == 2);
        Assert.Equal(1, await context.IoCTags.CountAsync(t => t.IocId == row.Id && t.TagId == tag.Id));
    }

    [Fact]
    public async Task Tags_created_in_an_early_chunk_are_reused_by_later_chunks()
    {
        const int batchSize = IocBatchWriter.ChunkSize + 1;
        var records = Enumerable.Range(0, batchSize)
            .Select(i => Rec($"r{i}", $"10.0.0.{i + 1}", tags: new[] { "shared" }))
            .ToList();

        var writer = CreateWriter();
        var result = await writer.ApplyAsync(records, PhishTankProviderValidator.PhishTankProviderId, CancellationToken.None);

        Assert.Equal(batchSize, result.Added);
        Assert.Equal(0, result.Updated);

        await using var context = fixture.CreateContext();
        var tag = Assert.Single(await context.Tags.Where(t => t.Name == "shared").ToListAsync());
        Assert.Equal(batchSize, await context.Iocs.CountAsync(i => i.ProviderId == 2));
        Assert.Equal(batchSize, await context.IoCTags.CountAsync(t => t.TagId == tag.Id));
    }

    [Fact]
    public async Task Removed_tags_disappear_but_the_shared_tag_row_survives()
    {
        var writer = CreateWriter();
        await writer.ApplyAsync(
            new[] { Rec("r1", "1.2.3.4", tags: new[] { "keep", "drop" }) },
            PhishTankProviderValidator.PhishTankProviderId,
            CancellationToken.None);

        await writer.ApplyAsync(
            new[] { Rec("r1", "1.2.3.4", tags: new[] { "keep" }) },
            PhishTankProviderValidator.PhishTankProviderId,
            CancellationToken.None);

        await using var context = fixture.CreateContext();
        var row = await context.Iocs.SingleAsync(i => i.ProviderId == 2);
        var keepTag = await context.Tags.SingleAsync(t => t.Name == "keep");
        var associations = await context.IoCTags.Where(t => t.IocId == row.Id).ToListAsync();
        var association = Assert.Single(associations);
        Assert.Equal(keepTag.Id, association.TagId);
        Assert.Contains(await context.Tags.Select(t => t.Name).ToListAsync(), name => name == "drop");
    }

    [Fact]
    public async Task Shared_tag_stays_attributed_to_each_source()
    {
        var writer = CreateWriter();
        await writer.ApplyAsync(
            new[] { Rec("r1", "1.2.3.4", tags: new[] { "shared" }), Rec("r2", "10.0.0.9", tags: new[] { "shared" }) },
            PhishTankProviderValidator.PhishTankProviderId,
            CancellationToken.None);

        await using var context = fixture.CreateContext();
        var tag = await context.Tags.SingleAsync(t => t.Name == "shared");
        var rows = await context.Iocs.Where(i => i.ProviderId == 2).ToListAsync();
        Assert.Equal(2, await context.IoCTags.CountAsync(t => t.TagId == tag.Id));
        Assert.All(rows, row => Assert.Contains(tag.Id, context.IoCTags.Where(t => t.IocId == row.Id).Select(t => t.TagId).ToHashSet()));
    }

    // --- TI-083 / FR-009 Whole-batch transaction ----------------------------------

    [Fact]
    public async Task Batch_larger_than_one_chunk_commits_atomically()
    {
        const int batchSize = IocBatchWriter.ChunkSize + 1;
        var records = Enumerable.Range(0, batchSize)
            .Select(i => Rec($"r{i}", $"10.0.0.{i + 1}"))
            .ToList();

        var writer = CreateWriter();
        var result = await writer.ApplyAsync(records, PhishTankProviderValidator.PhishTankProviderId, CancellationToken.None);

        Assert.Equal(batchSize, result.Added);
        Assert.Equal(0, result.Updated);

        await using var context = fixture.CreateContext();
        Assert.Equal(batchSize, await context.Iocs.CountAsync(i => i.ProviderId == 2));
    }

    [Fact]
    public async Task Conflict_in_a_later_chunk_rolls_back_every_ioc_and_tag_write()
    {
        await using (var seed = fixture.CreateContext())
        {
            seed.Iocs.AddRange(RecIoc("x", "1.2.3.4"), RecIoc("y", "10.0.0.9"));
            await seed.SaveChangesAsync();
        }

        const int firstChunk = IocBatchWriter.ChunkSize;
        var records = Enumerable.Range(0, firstChunk)
            .Select(i => Rec($"r{i}", $"10.0.1.{i + 1}", tags: new[] { "willRollback" }))
            .ToList();
        records.Add(Rec("x", "10.0.0.9", tags: new[] { "willRollback" }));

        var writer = CreateWriter();
        await Assert.ThrowsAsync<IocIdentityConflictException>(() =>
            writer.ApplyAsync(records, PhishTankProviderValidator.PhishTankProviderId, CancellationToken.None));

        await using var context = fixture.CreateContext();
        Assert.Equal(2, await context.Iocs.CountAsync(i => i.ProviderId == 2));
        Assert.Empty(await context.Tags.Where(t => t.Name == "willRollback").ToListAsync());
        Assert.Empty(await context.IoCTags.ToListAsync());
    }

    [Fact]
    public async Task Failed_batch_keeps_history_written_outside_the_ioc_transaction()
    {
        await using (var seed = fixture.CreateContext())
        {
            seed.Iocs.AddRange(RecIoc("x", "1.2.3.4"), RecIoc("y", "10.0.0.9"));
            await seed.SaveChangesAsync();
        }

        var writer = CreateWriter();
        var jobId = Guid.NewGuid();
        await Assert.ThrowsAsync<IocIdentityConflictException>(() =>
            writer.ApplyAsync(
                new[] { Rec("x", "10.0.0.9") },
                PhishTankProviderValidator.PhishTankProviderId,
                CancellationToken.None));

        var historyRepo = new FetchHistoryRepository(fixture.CreateContextFactory());
        await historyRepo.AddAsync(new FetchHistory(
            jobId, 2, FetchTrigger.Scheduled, FetchMode.Full, FetchStatus.Failed,
            FirstSeen, finishedAt: FirstSeen.AddMinutes(1),
            errorDetail: "Identity conflict for record 'x'"),
            CancellationToken.None);

        var history = await historyRepo.GetByJobIdAsync(jobId, CancellationToken.None);
        Assert.NotNull(history);
        Assert.Equal(FetchStatus.Failed, history!.Status);
        Assert.Equal(0, history.RecordsAdded);
        Assert.Equal(0, history.RecordsUpdated);
    }

    // --- TI-084 Redelivery idempotency ---------------------------------------------

    [Fact]
    public async Task Redelivered_batch_produces_the_same_final_dataset_without_duplicates()
    {
        var writer = CreateWriter();
        var records = new[]
        {
            Rec("r1", "1.2.3.4", tags: new[] { "a" }),
            Rec("r2", "10.0.0.9", tags: new[] { "b" })
        };

        var first = await writer.ApplyAsync(records, PhishTankProviderValidator.PhishTankProviderId, CancellationToken.None);
        var replay = await writer.ApplyAsync(records, PhishTankProviderValidator.PhishTankProviderId, CancellationToken.None);

        Assert.Equal((2, 0), (first.Added, first.Updated));
        Assert.Equal((0, 2), (replay.Added, replay.Updated));

        await using var context = fixture.CreateContext();
        Assert.Equal(2, await context.Iocs.CountAsync(i => i.ProviderId == 2));
        Assert.Equal(2, await context.IoCTags.CountAsync());
    }

    [Fact]
    public async Task Interleaved_redelivery_does_not_duplicate_rows()
    {
        var writer = CreateWriter();
        var batchA = new[] { Rec("r1", "1.2.3.4"), Rec("r2", "10.0.0.9") };
        var batchB = new[] { Rec("r1", "1.2.3.4"), Rec("r3", "10.0.1.7") };

        await writer.ApplyAsync(batchA, PhishTankProviderValidator.PhishTankProviderId, CancellationToken.None);
        await writer.ApplyAsync(batchB, PhishTankProviderValidator.PhishTankProviderId, CancellationToken.None);

        await using var context = fixture.CreateContext();
        Assert.Equal(3, await context.Iocs.CountAsync(i => i.ProviderId == 2));
    }

    // --- TI-085 False-positive state is refreshed by upsert ------------------------

    [Fact]
    public async Task Reappearing_row_refreshes_updated_at_and_clears_false_positive()
    {
        var evaluator = new FalsePositiveEvaluator(TimeProvider.System);
        await using (var seed = fixture.CreateContext())
        {
            seed.Iocs.Add(RecIoc("stale", "1.2.3.4", updatedAtUtc: DateTime.UtcNow.AddDays(-200)));
            await seed.SaveChangesAsync();
        }

        Assert.True(evaluator.IsFalsePositive(
            await StoredUpdatedAtAsync("stale")));

        var writer = CreateWriter();
        await writer.ApplyAsync(
            new[] { Rec("stale", "1.2.3.4") },
            PhishTankProviderValidator.PhishTankProviderId,
            CancellationToken.None);

        Assert.False(evaluator.IsFalsePositive(
            await StoredUpdatedAtAsync("stale")));
    }

    private async Task<DateTime> StoredUpdatedAtAsync(string recordId)
    {
        await using var context = fixture.CreateContext();
        return (await context.Iocs.SingleAsync(i => i.ProviderRecordId == recordId)).UpdatedAt;
    }

    private static IoC RecIoc(string recordId, string value, DateTime? updatedAtUtc = null)
    {
        var now = updatedAtUtc ?? FirstSeen;
        return new IoC(
            providerId: PhishTankProviderValidator.PhishTankProviderId,
            providerRecordId: recordId,
            iocValue: value,
            iocValueHash: Hasher.Hash(value),
            iocType: IocType.Ipv4,
            firstSeenUtc: FirstSeen,
            createdAt: now,
            updatedAt: now);
    }
}