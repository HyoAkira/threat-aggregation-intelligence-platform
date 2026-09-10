using System.IO.Compression;
using System.Text;
using Microsoft.EntityFrameworkCore;
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
        var handler = new StubHttpMessageHandler(System.Net.HttpStatusCode.TooManyRequests);
        var client = PhishTankTestClient.Create(handler);
        var mapper = new PhishTankRecordMapper();
        var parser = new PhishTankFeedParser(mapper);
        var validator = ProviderValidationFactory.CreateValidator(new PhishTankProviderValidator());
        var hasher = new Infrastructure.Hashing.Sha256ValueHasher();
        var contextFactory = new FakeDbContextFactory();

        var executor = new PhishTankFetchExecutor(client, parser, validator, hasher, contextFactory);

        var job = new FetchJob(Guid.NewGuid(), PhishTankProviderValidator.PhishTankProviderId, FetchTrigger.Manual, FetchMode.Full, 1);

        var ex = await Assert.ThrowsAsync<TerminalFetchException>(() => executor.ExecuteAsync(job, CancellationToken.None));
        Assert.Contains("429", ex.Message);
    }

    [Fact]
    public async Task Wrong_provider_id_throws_terminal()
    {
        var handler = new StubHttpMessageHandler(System.Net.HttpStatusCode.OK, "[]");
        var client = PhishTankTestClient.Create(handler);
        var mapper = new PhishTankRecordMapper();
        var parser = new PhishTankFeedParser(mapper);
        var validator = ProviderValidationFactory.CreateValidator(new PhishTankProviderValidator());
        var hasher = new Infrastructure.Hashing.Sha256ValueHasher();
        var contextFactory = new FakeDbContextFactory();

        var executor = new PhishTankFetchExecutor(client, parser, validator, hasher, contextFactory);

        var job = new FetchJob(Guid.NewGuid(), 99, FetchTrigger.Manual, FetchMode.Full, 1);

        var ex = await Assert.ThrowsAsync<TerminalFetchException>(() => executor.ExecuteAsync(job, CancellationToken.None));
        Assert.Contains("99", ex.Message);
    }

    [Fact]
    public async Task Wrong_mode_throws_terminal()
    {
        var handler = new StubHttpMessageHandler(System.Net.HttpStatusCode.OK, "[]");
        var client = PhishTankTestClient.Create(handler);
        var mapper = new PhishTankRecordMapper();
        var parser = new PhishTankFeedParser(mapper);
        var validator = ProviderValidationFactory.CreateValidator(new PhishTankProviderValidator());
        var hasher = new Infrastructure.Hashing.Sha256ValueHasher();
        var contextFactory = new FakeDbContextFactory();

        var executor = new PhishTankFetchExecutor(client, parser, validator, hasher, contextFactory);

        var job = new FetchJob(Guid.NewGuid(), PhishTankProviderValidator.PhishTankProviderId, FetchTrigger.Manual, FetchMode.Recent, 1);

        var ex = await Assert.ThrowsAsync<TerminalFetchException>(() => executor.ExecuteAsync(job, CancellationToken.None));
        Assert.Contains("Full", ex.Message);
    }

    [Fact]
    public async Task Authentication_failure_throws_terminal()
    {
        var handler = new StubHttpMessageHandler(System.Net.HttpStatusCode.Unauthorized);
        var client = PhishTankTestClient.Create(handler);
        var mapper = new PhishTankRecordMapper();
        var parser = new PhishTankFeedParser(mapper);
        var validator = ProviderValidationFactory.CreateValidator(new PhishTankProviderValidator());
        var hasher = new Infrastructure.Hashing.Sha256ValueHasher();
        var contextFactory = new FakeDbContextFactory();

        var executor = new PhishTankFetchExecutor(client, parser, validator, hasher, contextFactory);

        var job = new FetchJob(Guid.NewGuid(), PhishTankProviderValidator.PhishTankProviderId, FetchTrigger.Manual, FetchMode.Full, 1);

        var ex = await Assert.ThrowsAsync<TerminalFetchException>(() => executor.ExecuteAsync(job, CancellationToken.None));
        Assert.Contains("401", ex.Message);
    }

    [Fact]
    public async Task Server_error_throws_transient()
    {
        var handler = new StubHttpMessageHandler(System.Net.HttpStatusCode.BadGateway);
        var client = PhishTankTestClient.Create(handler);
        var mapper = new PhishTankRecordMapper();
        var parser = new PhishTankFeedParser(mapper);
        var validator = ProviderValidationFactory.CreateValidator(new PhishTankProviderValidator());
        var hasher = new Infrastructure.Hashing.Sha256ValueHasher();
        var contextFactory = new FakeDbContextFactory();

        var executor = new PhishTankFetchExecutor(client, parser, validator, hasher, contextFactory);

        var job = new FetchJob(Guid.NewGuid(), PhishTankProviderValidator.PhishTankProviderId, FetchTrigger.Manual, FetchMode.Full, 1);

        var ex = await Assert.ThrowsAsync<Application.Fetches.ProviderFetchHttpException>(() => executor.ExecuteAsync(job, CancellationToken.None));
        Assert.Equal(System.Net.HttpStatusCode.BadGateway, ex.StatusCode);
        Assert.True(FetchErrorClassifier.IsTransient(ex));
    }
}

internal sealed class FakeDbContextFactory : IDbContextFactory<Infrastructure.Persistence.ThreatIntelligenceDbContext>
{
    public Infrastructure.Persistence.ThreatIntelligenceDbContext CreateDbContext()
        => throw new NotSupportedException("Fake — used only for unit tests that never reach the database.");

    public Task<Infrastructure.Persistence.ThreatIntelligenceDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Fake — used only for unit tests that never reach the database.");
}
