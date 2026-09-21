using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using ThreatIntelligence.Application.BatchWriter;
using ThreatIntelligence.Application.Fetches;
using ThreatIntelligence.Application.ProviderRecords;
using ThreatIntelligence.Domain.Enums;
using ThreatIntelligence.Infrastructure.PhishTank;
using ThreatIntelligence.UnitTests.ProviderRecords;

namespace ThreatIntelligence.UnitTests.PhishTank;

public sealed class PhishTankFetchExecutorTests
{
    [Fact]
    public async Task Rate_limit_throws_terminal_fetch_exception()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.TooManyRequests);
        var client = PhishTankTestClient.Create(handler);
        var mapper = new PhishTankRecordMapper();
        var parser = new PhishTankFeedParser(mapper);
        var validator = ProviderValidationFactory.CreateValidator(new PhishTankProviderValidator());
        var batchWriter = new StubBatchWriter();

        var executor = new PhishTankFetchExecutor(client, parser, validator, batchWriter);

        var job = new FetchJob(Guid.NewGuid(), PhishTankProviderValidator.PhishTankProviderId, FetchTrigger.Manual, FetchMode.Full, 1);

        var ex = await Assert.ThrowsAsync<TerminalFetchException>(() => executor.ExecuteAsync(job, CancellationToken.None));
        Assert.Contains("429", ex.Message);
    }

    [Fact]
    public async Task Wrong_provider_id_throws_terminal()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, "[]");
        var client = PhishTankTestClient.Create(handler);
        var mapper = new PhishTankRecordMapper();
        var parser = new PhishTankFeedParser(mapper);
        var validator = ProviderValidationFactory.CreateValidator(new PhishTankProviderValidator());
        var batchWriter = new StubBatchWriter();

        var executor = new PhishTankFetchExecutor(client, parser, validator, batchWriter);

        var job = new FetchJob(Guid.NewGuid(), 99, FetchTrigger.Manual, FetchMode.Full, 1);

        var ex = await Assert.ThrowsAsync<TerminalFetchException>(() => executor.ExecuteAsync(job, CancellationToken.None));
        Assert.Contains("99", ex.Message);
    }

    [Fact]
    public async Task Wrong_mode_throws_terminal()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, "[]");
        var client = PhishTankTestClient.Create(handler);
        var mapper = new PhishTankRecordMapper();
        var parser = new PhishTankFeedParser(mapper);
        var validator = ProviderValidationFactory.CreateValidator(new PhishTankProviderValidator());
        var batchWriter = new StubBatchWriter();

        var executor = new PhishTankFetchExecutor(client, parser, validator, batchWriter);

        var job = new FetchJob(Guid.NewGuid(), PhishTankProviderValidator.PhishTankProviderId, FetchTrigger.Manual, FetchMode.Recent, 1);

        var ex = await Assert.ThrowsAsync<TerminalFetchException>(() => executor.ExecuteAsync(job, CancellationToken.None));
        Assert.Contains("Full", ex.Message);
    }

    [Fact]
    public async Task Authentication_failure_throws_terminal()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.Unauthorized);
        var client = PhishTankTestClient.Create(handler);
        var mapper = new PhishTankRecordMapper();
        var parser = new PhishTankFeedParser(mapper);
        var validator = ProviderValidationFactory.CreateValidator(new PhishTankProviderValidator());
        var batchWriter = new StubBatchWriter();

        var executor = new PhishTankFetchExecutor(client, parser, validator, batchWriter);

        var job = new FetchJob(Guid.NewGuid(), PhishTankProviderValidator.PhishTankProviderId, FetchTrigger.Manual, FetchMode.Full, 1);

        var ex = await Assert.ThrowsAsync<TerminalFetchException>(() => executor.ExecuteAsync(job, CancellationToken.None));
        Assert.Contains("401", ex.Message);
    }

    [Fact]
    public async Task Server_error_throws_transient()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.BadGateway);
        var client = PhishTankTestClient.Create(handler);
        var mapper = new PhishTankRecordMapper();
        var parser = new PhishTankFeedParser(mapper);
        var validator = ProviderValidationFactory.CreateValidator(new PhishTankProviderValidator());
        var batchWriter = new StubBatchWriter();

        var executor = new PhishTankFetchExecutor(client, parser, validator, batchWriter);

        var job = new FetchJob(Guid.NewGuid(), PhishTankProviderValidator.PhishTankProviderId, FetchTrigger.Manual, FetchMode.Full, 1);

        var ex = await Assert.ThrowsAsync<ProviderFetchHttpException>(() => executor.ExecuteAsync(job, CancellationToken.None));
        Assert.Equal(HttpStatusCode.BadGateway, ex.StatusCode);
        Assert.True(FetchErrorClassifier.IsTransient(ex));
    }

    [Fact]
    public async Task Record_failing_ioc_validation_is_skipped_and_valid_records_are_applied()
    {
        var feedJson = JsonSerializer.Serialize(new object[]
        {
            new { phish_id = 9502068, url = "not a url", submission_time = "2026-01-01T12:00:00+00:00" },
            new { phish_id = 9502069, url = "http://evil.example/phish", submission_time = "2026-01-01T12:00:00+00:00" }
        });
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(GzipOf(feedJson))
        });
        var client = PhishTankTestClient.Create(handler);
        var mapper = new PhishTankRecordMapper();
        var parser = new PhishTankFeedParser(mapper);
        var validator = ProviderValidationFactory.CreateValidator(new PhishTankProviderValidator());
        var applied = new List<NormalizedProviderRecord>();
        var batchWriter = new StubBatchWriter
        {
            Behavior = (records, _, _) =>
            {
                applied.AddRange(records);
                return Task.FromResult(new IocBatchResult(records.Count, 0));
            }
        };

        var executor = new PhishTankFetchExecutor(client, parser, validator, batchWriter);
        var job = new FetchJob(Guid.NewGuid(), PhishTankProviderValidator.PhishTankProviderId, FetchTrigger.Manual, FetchMode.Full, 1);

        var result = await executor.ExecuteAsync(job, CancellationToken.None);

        Assert.Single(applied);
        Assert.Equal("9502069", applied[0].ProviderRecordId);
        Assert.Equal(1, result.RecordsAdded);
    }

    [Fact]
    public async Task Malformed_record_is_skipped_and_valid_records_are_applied()
    {
        var feedJson = JsonSerializer.Serialize(new object[]
        {
            new { phish_id = (int?)null, url = "http://bad.example" },
            new { phish_id = 9502069, url = "http://evil.example/phish", submission_time = "2026-01-01T12:00:00+00:00" }
        });
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(GzipOf(feedJson))
        });
        var client = PhishTankTestClient.Create(handler);
        var mapper = new PhishTankRecordMapper();
        var parser = new PhishTankFeedParser(mapper);
        var validator = ProviderValidationFactory.CreateValidator(new PhishTankProviderValidator());
        var applied = new List<NormalizedProviderRecord>();
        var batchWriter = new StubBatchWriter
        {
            Behavior = (records, _, _) =>
            {
                applied.AddRange(records);
                return Task.FromResult(new IocBatchResult(records.Count, 0));
            }
        };

        var executor = new PhishTankFetchExecutor(client, parser, validator, batchWriter);
        var job = new FetchJob(Guid.NewGuid(), PhishTankProviderValidator.PhishTankProviderId, FetchTrigger.Manual, FetchMode.Full, 1);

        var result = await executor.ExecuteAsync(job, CancellationToken.None);

        Assert.Single(applied);
        Assert.Equal("9502069", applied[0].ProviderRecordId);
        Assert.Equal(1, result.RecordsAdded);
    }

    private static MemoryStream GzipOf(string json)
    {
        var stream = new MemoryStream();
        using (var gzip = new System.IO.Compression.GZipStream(stream, System.IO.Compression.CompressionMode.Compress, leaveOpen: true))
        using (var writer = new StreamWriter(gzip))
        {
            writer.Write(json);
        }

        stream.Position = 0;
        return stream;
    }
}

internal sealed class StubBatchWriter : IIocBatchWriter
{
    public Func<IReadOnlyList<NormalizedProviderRecord>, int, CancellationToken, Task<IocBatchResult>>? Behavior { get; init; }

    public Task<IocBatchResult> ApplyAsync(
        IReadOnlyList<NormalizedProviderRecord> records,
        int providerId,
        CancellationToken cancellationToken)
        => Behavior?.Invoke(records, providerId, cancellationToken)
           ?? Task.FromResult(new IocBatchResult(0, 0));
}
