using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ThreatIntelligence.Application.BatchWriter;
using ThreatIntelligence.Application.Fetches;
using ThreatIntelligence.Application.Normalization;
using ThreatIntelligence.Application.ProviderRecords;
using ThreatIntelligence.Domain.Enums;
using ThreatIntelligence.Infrastructure.Hashing;
using ThreatIntelligence.Infrastructure.PhishTank;
using ThreatIntelligence.Infrastructure.Pipeline;
using ThreatIntelligence.Infrastructure.Persistence;
using ThreatIntelligence.Infrastructure.PublicSuffix;

namespace ThreatIntelligence.IntegrationTests;

[Collection("sqlserver")]
public sealed class PhishTankFetchExecutorTests : IAsyncLifetime
{
    private readonly SqlServerFixture _fixture;

    public PhishTankFetchExecutorTests(SqlServerFixture fixture) => _fixture = fixture;

    public Task InitializeAsync() => _fixture.ResetDataAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private static MemoryStream GzipStreamOf(string json)
    {
        var stream = new MemoryStream();
        using (var gzip = new GZipStream(stream, CompressionMode.Compress, leaveOpen: true))
        using (var writer = new StreamWriter(gzip, Encoding.UTF8))
        {
            writer.Write(json);
        }
        stream.Position = 0;
        return stream;
    }

    private static string FeedJson(params object[] records)
    {
        return JsonSerializer.Serialize(records);
    }

    private static object ValidRecord(int id, string url = "http://evil.example/phish") => new
    {
        phish_id = id,
        url,
        phish_detail_url = $"http://data.phishtank.com/data/{id}/detail",
        submission_time = "2026-01-01T12:00:00+00:00",
        verification_time = "2026-01-01T12:30:00+00:00",
        verified = "yes",
        online = "yes",
        target = "Example Bank",
        details = new[]
        {
            new { ip_address = "1.2.3.4", cidr_block = "1.2.3.0/24", announcing_network = "1234", rir = "arin", detail_time = "2026-01-01T12:30:00+00:00" }
        }
    };

    private PhishTankFetchExecutor CreateExecutor(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handlerFunc)
    {
        var handler = new DelegatingHandlerStub(handlerFunc);
        var httpClient = new HttpClient(handler) { BaseAddress = null };
        var client = new PhishTankClient(httpClient, new Infrastructure.Configuration.PhishTankOptions());
        var mapper = new PhishTankRecordMapper();
        var parser = new PhishTankFeedParser(mapper);
        var resolver = new PublicSuffixParentDomainResolver();
        var classifier = new IocClassifier(
            new Ipv4Normalizer(), new Ipv6Normalizer(),
            new UrlNormalizer(resolver), new DomainNormalizer(resolver));
        var validator = new ProviderRecordValidator(
            classifier,
            new Dictionary<int, IProviderValidator>
            {
                [PhishTankProviderValidator.PhishTankProviderId] = new PhishTankProviderValidator()
            });
        var hasher = new Sha256ValueHasher();
        var context = _fixture.CreateContext();
        var upsertMapper = new PhishTankUpsertFieldsMapper(hasher, TimeProvider.System);
        var mappers = new Dictionary<int, IIocUpsertFieldsMapper>
        {
            [PhishTankProviderValidator.PhishTankProviderId] = upsertMapper
        };
        var batchWriter = new IocBatchWriter(context, mappers);
        return new PhishTankFetchExecutor(client, parser, validator, batchWriter);
    }

    [Fact]
    public async Task Valid_fixture_commits_once_with_correct_counters()
    {
        var json = FeedJson(ValidRecord(1, "http://evil.example/phish/1"), ValidRecord(2, "http://evil.example/phish/2"), ValidRecord(3, "http://evil.example/phish/3"));
        var executor = CreateExecutor((_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(GzipStreamOf(json))
            };
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/gzip");
            return Task.FromResult(response);
        });

        var job = new FetchJob(Guid.NewGuid(), 2, FetchTrigger.Manual, FetchMode.Full, 1);
        var result = await executor.ExecuteAsync(job, CancellationToken.None);

        Assert.Equal(3, result.RecordsAdded);
        Assert.Equal(0, result.RecordsUpdated);

        await using var context = _fixture.CreateContext();
        var iocs = await context.Iocs.Where(i => i.ProviderId == 2).ToListAsync();
        Assert.Equal(3, iocs.Count);
    }

    [Fact]
    public async Task Idempotent_rerun_updates_zero_records()
    {
        var json = FeedJson(ValidRecord(1, "http://evil.example/phish/1"), ValidRecord(2, "http://evil.example/phish/2"));
        var callCount = 0;
        var executor = CreateExecutor((_, _) =>
        {
            Interlocked.Increment(ref callCount);
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(GzipStreamOf(json))
            };
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/gzip");
            return Task.FromResult(response);
        });

        var jobId = Guid.NewGuid();
        var job1 = new FetchJob(jobId, 2, FetchTrigger.Manual, FetchMode.Full, 1);
        var result1 = await executor.ExecuteAsync(job1, CancellationToken.None);

        var job2 = new FetchJob(jobId, 2, FetchTrigger.Manual, FetchMode.Full, 2);
        var result2 = await executor.ExecuteAsync(job2, CancellationToken.None);

        Assert.Equal(2, result1.RecordsAdded);
        Assert.Equal(0, result1.RecordsUpdated);
        Assert.Equal(0, result2.RecordsAdded);
        Assert.Equal(2, result2.RecordsUpdated);
    }

    [Fact]
    public async Task Malformed_final_record_is_skipped_and_valid_records_persist()
    {
        var json = FeedJson(ValidRecord(1, "http://evil.example/phish/1"), ValidRecord(2, "http://evil.example/phish/2"), new { phish_id = (int?)null, url = "http://bad.example" });
        var executor = CreateExecutor((_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(GzipStreamOf(json))
            };
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/gzip");
            return Task.FromResult(response);
        });

        var job = new FetchJob(Guid.NewGuid(), 2, FetchTrigger.Manual, FetchMode.Full, 1);

        var result = await executor.ExecuteAsync(job, CancellationToken.None);

        Assert.Equal(2, result.RecordsAdded);
        await using var context = _fixture.CreateContext();
        var iocs = await context.Iocs.Where(i => i.ProviderId == 2).ToListAsync();
        Assert.Equal(2, iocs.Count);
    }

    [Fact]
    public async Task Rate_limit_returns_zero_counters()
    {
        var executor = CreateExecutor((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests)));

        var job = new FetchJob(Guid.NewGuid(), 2, FetchTrigger.Manual, FetchMode.Full, 1);

        var ex = await Assert.ThrowsAsync<TerminalFetchException>(() => executor.ExecuteAsync(job, CancellationToken.None));
        Assert.Contains("429", ex.Message);

        await using var context = _fixture.CreateContext();
        var iocs = await context.Iocs.Where(i => i.ProviderId == 2).ToListAsync();
        Assert.Empty(iocs);
    }

    private sealed class DelegatingHandlerStub(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handlerFunc) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => handlerFunc(request, cancellationToken);
    }
}
