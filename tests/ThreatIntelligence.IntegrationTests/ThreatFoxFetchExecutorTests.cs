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
using ThreatIntelligence.Infrastructure.Pipeline;
using ThreatIntelligence.Infrastructure.PublicSuffix;
using ThreatIntelligence.Infrastructure.ThreatFox;

namespace ThreatIntelligence.IntegrationTests;

[Collection("sqlserver")]
public sealed class ThreatFoxFetchExecutorTests : IAsyncLifetime
{
    private readonly SqlServerFixture _fixture;

    public ThreatFoxFetchExecutorTests(SqlServerFixture fixture) => _fixture = fixture;

    public Task InitializeAsync() => _fixture.ResetDataAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private static string RecentJson(params object[] records) => JsonSerializer.Serialize(records);

    private static MemoryStream ZipStreamOf(string json)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("full.json");
            using var entryStream = entry.Open();
            using var writer = new StreamWriter(entryStream, Encoding.UTF8);
            writer.Write(json);
        }

        stream.Position = 0;
        return stream;
    }

    private static object ValidDomainRecord(string id, string domain = "evil.example.com") => new
    {
        id,
        ioc = domain,
        ioc_type = "domain",
        threat_type = "botnet_cc",
        threat_type_desc = "Command and Control",
        confidence_level = 80,
        first_seen = "2026-01-01T00:00:00UTC",
        last_seen = "2026-01-02T00:00:00UTC",
        reference = $"https://threatfox.abuse.ch/ioc/{id}",
        reporter = "tester",
        is_compromised = false,
        tags = (string[]?)null,
        malware = "MalwareX",
        malware_printable = "MalwareX",
        malware_alias = "mx",
        malware_malpedia = "https://malpedia.caad.fkie.fraunhofer.de/details/malware.malwrex",
        ioc_type_desc = "the domain to detect",
        malware_samples = Array.Empty<object>()
    };

    private static object ValidUrlRecord(string id, string url = "http://evil.example.com/path") => new
    {
        id,
        ioc = url,
        ioc_type = "url",
        threat_type = "botnet_cc",
        threat_type_desc = "Command and Control",
        confidence_level = 75,
        first_seen = "2026-01-01T00:00:00UTC",
        last_seen = "2026-01-02T00:00:00UTC",
        reference = $"https://threatfox.abuse.ch/ioc/{id}",
        reporter = "tester",
        is_compromised = true,
        tags = (string[]?)null,
        malware = "MalwareX",
        malware_printable = "MalwareX",
        ioc_type_desc = "the URL to detect",
        malware_samples = Array.Empty<object>()
    };

    private static object ValidIpPortRecord(string id, string ipPort = "1.2.3.4:443") => new
    {
        id,
        ioc = ipPort,
        ioc_type = "ip:port",
        threat_type = "botnet_cc",
        threat_type_desc = "Command and Control",
        confidence_level = 90,
        first_seen = "2026-01-01T00:00:00UTC",
        last_seen = "2026-01-02T00:00:00UTC",
        reference = $"https://threatfox.abuse.ch/ioc/{id}",
        reporter = "tester",
        is_compromised = false,
        tags = (string[]?)null,
        malware = "MalwareX",
        ioc_type_desc = "the IP:port to detect",
        malware_samples = Array.Empty<object>()
    };

    private ThreatFoxFetchExecutor CreateExecutor(Func<ThreatFoxRecentKind, string> contentProvider)
    {
        var client = new StubThreatFoxExportClient(contentProvider);
        var mapper = new ThreatFoxRecordMapper();
        var recentParser = new ThreatFoxRecentParser(mapper);
        var fullParser = new ThreatFoxFullParser(recentParser);
        var resolver = new PublicSuffixParentDomainResolver();
        var classifier = new IocClassifier(
            new Ipv4Normalizer(), new Ipv6Normalizer(),
            new UrlNormalizer(resolver), new DomainNormalizer(resolver));
        var validator = new ProviderRecordValidator(
            classifier,
            new Dictionary<int, IProviderValidator>
            {
                [ThreatFoxRecordMapper.ThreatFoxProviderId] = new ThreatFoxProviderValidator()
            });
        var hasher = new Sha256ValueHasher();
        var context = _fixture.CreateContext();
        var upsertMapper = new ThreatFoxUpsertFieldsMapper(hasher, TimeProvider.System);
        var mappers = new Dictionary<int, IIocUpsertFieldsMapper>
        {
            [ThreatFoxRecordMapper.ThreatFoxProviderId] = upsertMapper
        };
        var batchWriter = new IocBatchWriter(context, mappers);
        return new ThreatFoxFetchExecutor(client, recentParser, fullParser, validator, batchWriter);
    }

    [Fact]
    public async Task Valid_recent_fixture_commits_once_with_correct_counters()
    {
        var domainJson = RecentJson(ValidDomainRecord("tf-1"), ValidDomainRecord("tf-2"));
        var urlJson = RecentJson(ValidUrlRecord("tf-3"));

        var executor = CreateExecutor(kind => kind switch
        {
            ThreatFoxRecentKind.Domains => domainJson,
            ThreatFoxRecentKind.Urls => urlJson,
            ThreatFoxRecentKind.IpPort => "[]",
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        });

        var job = new FetchJob(Guid.NewGuid(), ThreatFoxRecordMapper.ThreatFoxProviderId, FetchTrigger.Manual, FetchMode.Recent, 1);
        var result = await executor.ExecuteAsync(job, CancellationToken.None);

        Assert.Equal(2, result.RecordsAdded);
        Assert.Equal(1, result.RecordsUpdated);

        await using var context = _fixture.CreateContext();
        var iocs = await context.Iocs.Where(i => i.ProviderId == ThreatFoxRecordMapper.ThreatFoxProviderId).ToListAsync();
        Assert.Equal(2, iocs.Count);
    }

    [Fact]
    public async Task Keyed_object_recent_export_commits_records_from_the_real_export_shape()
    {
        var ipPortJson = """{"1911444":[{"ioc_value":"154.91.62.102:8074","ioc_type":"ip:port","threat_type":"botnet_cc","malware":"win.vshell","malware_printable":"VShell","first_seen_utc":"2026-09-10 23:05:07","confidence_level":100,"is_compromised":true,"tags":"vshell","reporter":"anonymous"}]}""";

        var executor = CreateExecutor(kind => kind switch
        {
            ThreatFoxRecentKind.Domains => "{}",
            ThreatFoxRecentKind.Urls => "{}",
            ThreatFoxRecentKind.IpPort => ipPortJson,
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        });

        var job = new FetchJob(Guid.NewGuid(), ThreatFoxRecordMapper.ThreatFoxProviderId, FetchTrigger.Manual, FetchMode.Recent, 1);
        var result = await executor.ExecuteAsync(job, CancellationToken.None);

        Assert.Equal(1, result.RecordsAdded);
        Assert.Equal(0, result.RecordsUpdated);

        await using var context = _fixture.CreateContext();
        var ioc = Assert.Single(await context.Iocs.Where(i => i.ProviderId == ThreatFoxRecordMapper.ThreatFoxProviderId).ToListAsync());
        Assert.Equal("1911444", ioc.ProviderRecordId);
        Assert.Equal("154.91.62.102", ioc.IocValue);
        Assert.Equal(8074, ioc.Port);
        Assert.Equal<byte?>(100, ioc.ThreatFoxConfidenceLevel);
    }

    [Fact]
    public async Task Idempotent_rerun_updates_zero_records()
    {
        var domainJson = RecentJson(ValidDomainRecord("tf-1"), ValidDomainRecord("tf-2"));
        var urlJson = RecentJson(ValidUrlRecord("tf-3"));

        var executor = CreateExecutor(kind => kind switch
        {
            ThreatFoxRecentKind.Domains => domainJson,
            ThreatFoxRecentKind.Urls => urlJson,
            ThreatFoxRecentKind.IpPort => "[]",
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        });

        var jobId = Guid.NewGuid();
        var job1 = new FetchJob(jobId, ThreatFoxRecordMapper.ThreatFoxProviderId, FetchTrigger.Manual, FetchMode.Recent, 1);
        var result1 = await executor.ExecuteAsync(job1, CancellationToken.None);

        var job2 = new FetchJob(jobId, ThreatFoxRecordMapper.ThreatFoxProviderId, FetchTrigger.Manual, FetchMode.Recent, 2);
        var result2 = await executor.ExecuteAsync(job2, CancellationToken.None);

        Assert.Equal(2, result1.RecordsAdded);
        Assert.Equal(1, result1.RecordsUpdated);
        Assert.Equal(0, result2.RecordsAdded);
        Assert.Equal(3, result2.RecordsUpdated);
    }

    [Fact]
    public async Task Bad_record_rolls_back_entire_batch()
    {
        var domainJson = RecentJson(
            ValidDomainRecord("tf-1"),
            new { id = (string?)null, ioc = "evil.example.com", ioc_type = "domain" });

        var executor = CreateExecutor(kind => kind switch
        {
            ThreatFoxRecentKind.Domains => domainJson,
            ThreatFoxRecentKind.Urls => "[]",
            ThreatFoxRecentKind.IpPort => "[]",
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        });

        var job = new FetchJob(Guid.NewGuid(), ThreatFoxRecordMapper.ThreatFoxProviderId, FetchTrigger.Manual, FetchMode.Recent, 1);

        await Assert.ThrowsAsync<TerminalFetchException>(() => executor.ExecuteAsync(job, CancellationToken.None));

        await using var context = _fixture.CreateContext();
        var iocs = await context.Iocs.Where(i => i.ProviderId == ThreatFoxRecordMapper.ThreatFoxProviderId).ToListAsync();
        Assert.Empty(iocs);
    }

    [Fact]
    public async Task Wrong_provider_throws_terminal()
    {
        var executor = CreateExecutor(_ => "[]");
        var job = new FetchJob(Guid.NewGuid(), 99, FetchTrigger.Manual, FetchMode.Recent, 1);

        var ex = await Assert.ThrowsAsync<TerminalFetchException>(() => executor.ExecuteAsync(job, CancellationToken.None));
        Assert.Contains("99", ex.Message);
    }

    [Fact]
    public async Task Full_mode_fetches_zip_and_commits()
    {
        var json = RecentJson(ValidDomainRecord("tf-1"));
        var client = new StubThreatFoxExportClient(_ => throw new InvalidOperationException("should not be called for full"));
        var fullJson = RecentJson(ValidDomainRecord("tf-f1"), ValidDomainRecord("tf-f2"));
        client = new StubThreatFoxExportClient(fullJson);
        var mapper = new ThreatFoxRecordMapper();
        var recentParser = new ThreatFoxRecentParser(mapper);
        var fullParser = new ThreatFoxFullParser(recentParser);
        var resolver = new PublicSuffixParentDomainResolver();
        var classifier = new IocClassifier(
            new Ipv4Normalizer(), new Ipv6Normalizer(),
            new UrlNormalizer(resolver), new DomainNormalizer(resolver));
        var validator = new ProviderRecordValidator(
            classifier,
            new Dictionary<int, IProviderValidator>
            {
                [ThreatFoxRecordMapper.ThreatFoxProviderId] = new ThreatFoxProviderValidator()
            });
        var hasher = new Sha256ValueHasher();
        var context = _fixture.CreateContext();
        var upsertMapper = new ThreatFoxUpsertFieldsMapper(hasher, TimeProvider.System);
        var mappers = new Dictionary<int, IIocUpsertFieldsMapper>
        {
            [ThreatFoxRecordMapper.ThreatFoxProviderId] = upsertMapper
        };
        var batchWriter = new IocBatchWriter(context, mappers);
        var executor = new ThreatFoxFetchExecutor(client, recentParser, fullParser, validator, batchWriter);

        var job = new FetchJob(Guid.NewGuid(), ThreatFoxRecordMapper.ThreatFoxProviderId, FetchTrigger.Manual, FetchMode.Full, 1);
        var result = await executor.ExecuteAsync(job, CancellationToken.None);

        Assert.Equal(1, result.RecordsAdded);
        Assert.Equal(1, result.RecordsUpdated);

        await using var ctx = _fixture.CreateContext();
        var iocs = await ctx.Iocs.Where(i => i.ProviderId == ThreatFoxRecordMapper.ThreatFoxProviderId).ToListAsync();
        Assert.Equal(1, iocs.Count);
    }

    [Fact]
    public async Task Http_error_throws_terminal()
    {
        var client = new StubThreatFoxExportClient(_ => throw new ProviderFetchHttpException(HttpStatusCode.Unauthorized, "ThreatFox export request was rejected."));
        var mapper = new ThreatFoxRecordMapper();
        var recentParser = new ThreatFoxRecentParser(mapper);
        var fullParser = new ThreatFoxFullParser(recentParser);
        var resolver = new PublicSuffixParentDomainResolver();
        var classifier = new IocClassifier(
            new Ipv4Normalizer(), new Ipv6Normalizer(),
            new UrlNormalizer(resolver), new DomainNormalizer(resolver));
        var validator = new ProviderRecordValidator(
            classifier,
            new Dictionary<int, IProviderValidator>
            {
                [ThreatFoxRecordMapper.ThreatFoxProviderId] = new ThreatFoxProviderValidator()
            });
        var hasher = new Sha256ValueHasher();
        var context = _fixture.CreateContext();
        var upsertMapper = new ThreatFoxUpsertFieldsMapper(hasher, TimeProvider.System);
        var mappers = new Dictionary<int, IIocUpsertFieldsMapper>
        {
            [ThreatFoxRecordMapper.ThreatFoxProviderId] = upsertMapper
        };
        var batchWriter = new IocBatchWriter(context, mappers);
        var executor = new ThreatFoxFetchExecutor(client, recentParser, fullParser, validator, batchWriter);

        var job = new FetchJob(Guid.NewGuid(), ThreatFoxRecordMapper.ThreatFoxProviderId, FetchTrigger.Manual, FetchMode.Recent, 1);

        await Assert.ThrowsAsync<TerminalFetchException>(() => executor.ExecuteAsync(job, CancellationToken.None));
    }

    private sealed class DelegatingHandlerStub(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handlerFunc) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => handlerFunc(request, cancellationToken);
    }

    private sealed class StubThreatFoxExportClient : IThreatFoxExportClient
    {
        private readonly Func<ThreatFoxRecentKind, string>? _recentContentProvider;
        private readonly string? _fullJson;

        public StubThreatFoxExportClient(Func<ThreatFoxRecentKind, string> recentContentProvider)
        {
            _recentContentProvider = recentContentProvider;
        }

        public StubThreatFoxExportClient(string fullJson)
        {
            _fullJson = fullJson;
        }

        public Task<ThreatFoxExportStream> FetchRecentAsync(ThreatFoxRecentKind kind, CancellationToken cancellationToken)
        {
            var json = _recentContentProvider!(kind);
            var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
            return Task.FromResult(new ThreatFoxExportStream($"{kind}_recent.json", stream));
        }

        public Task<ThreatFoxExportStream> FetchFullAsync(CancellationToken cancellationToken)
        {
            var zipStream = ZipStreamOf(_fullJson!);
            return Task.FromResult(new ThreatFoxExportStream("full.json.zip", zipStream));
        }
    }
}
