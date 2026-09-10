using System.Net;
using System.Net.Http;
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
