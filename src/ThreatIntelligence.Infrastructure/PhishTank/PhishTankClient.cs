using System.Diagnostics;
using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ThreatIntelligence.Application.Fetches;
using ThreatIntelligence.Infrastructure.Configuration;

namespace ThreatIntelligence.Infrastructure.PhishTank;

public sealed class PhishTankClient : IPhishTankClient
{
    public const string DefaultBaseUrl = "https://data.phishtank.com";
    private const string FeedPathPrefix = "data";
    private const string FeedFileName = "online-valid.json.gz";

    private readonly HttpClient _httpClient;
    private readonly string? _legacyAppKey;
    private readonly string _baseUrl;
    private readonly ILogger<PhishTankClient> _logger;

    public PhishTankClient(
        HttpClient httpClient,
        PhishTankOptions options,
        string baseUrl = DefaultBaseUrl,
        ILogger<PhishTankClient>? logger = null)
    {
        _httpClient = httpClient;
        _legacyAppKey = options.AppKey;
        _baseUrl = baseUrl;
        _logger = logger ?? NullLogger<PhishTankClient>.Instance;
        if (_httpClient.DefaultRequestHeaders.UserAgent.Count > 0) return;
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("ThreatIntelligence.Worker/1.0");
    }

    public async Task<PhishTankFeedStream> FetchFeedAsync(CancellationToken cancellationToken)
        => await FetchFeedAsync(_legacyAppKey, cancellationToken).ConfigureAwait(false);

    public async Task<PhishTankFeedStream> FetchFeedAsync(string? appKey, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, BuildFeedUri(appKey));

        _logger.LogInformation("PhishTank feed request starting FileName={FileName}", FeedFileName);

        var stopwatch = Stopwatch.StartNew();
        var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        stopwatch.Stop();

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            _logger.LogError("PhishTank feed rate limit exceeded (HTTP 429).");
            throw new PhishTankQuotaException("PhishTank rate limit exceeded (HTTP 429).");
        }

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            _logger.LogError(
                "PhishTank feed authentication failed StatusCode={StatusCode} ElapsedMs={ElapsedMs}",
                (int)response.StatusCode,
                stopwatch.ElapsedMilliseconds);
            throw new PhishTankAuthenticationException(
                $"PhishTank authentication failed (HTTP {(int)response.StatusCode}).");
        }

        if (!response.IsSuccessStatusCode)
        {
            var payloadPrefix = await ReadDiagnosticPrefixAsync(response, cancellationToken).ConfigureAwait(false);
            _logger.LogError(
                "PhishTank feed request failed StatusCode={StatusCode} ElapsedMs={ElapsedMs} Location={Location} PayloadPrefix={PayloadPrefix}",
                (int)response.StatusCode,
                stopwatch.ElapsedMilliseconds,
                response.Headers.Location is null ? "<none>" : "<present>",
                payloadPrefix);

            if ((int)response.StatusCode is >= 300 and < 400)
            {
                throw new ProviderFetchHttpException(
                    response.StatusCode,
                    $"PhishTank feed request was redirected (HTTP {(int)response.StatusCode}); the feed URL may have moved.");
            }

            throw response.StatusCode >= HttpStatusCode.InternalServerError
                ? new ProviderFetchHttpException(response.StatusCode, "PhishTank feed request failed.")
                : new ProviderFetchHttpException(response.StatusCode, "PhishTank feed request was rejected.");
        }

        _logger.LogInformation(
            "PhishTank feed response received FileName={FileName} StatusCode={StatusCode} ContentType={ContentType} ContentLength={ContentLength} ElapsedMs={ElapsedMs}",
            FeedFileName,
            (int)response.StatusCode,
            response.Content.Headers.ContentType?.ToString(),
            response.Content.Headers.ContentLength,
            stopwatch.ElapsedMilliseconds);

        var content = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return new PhishTankFeedStream(FeedFileName, content);
    }

    internal Uri BuildFeedUri(string? appKey = null)
    {
        var baseUri = new Uri(_baseUrl.TrimEnd('/'));
        var path = string.IsNullOrEmpty(appKey)
            ? $"{FeedPathPrefix}/{FeedFileName}"
            : $"{FeedPathPrefix}/{appKey}/{FeedFileName}";
        return new Uri(baseUri, path);
    }

    private static async Task<string> ReadDiagnosticPrefixAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var body = await response.Content
                .ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);

            var buffer = new byte[512];
            var read = await body.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return "<empty body>";
            }

            var text = Encoding.UTF8.GetString(buffer, 0, read).Replace('\r', ' ').Replace('\n', ' ').Trim();
            return text.Length > 0 ? text : "<non-text body>";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return "<unreadable body>";
        }
    }
}

public interface IPhishTankClient
{
    Task<PhishTankFeedStream> FetchFeedAsync(CancellationToken cancellationToken);

    Task<PhishTankFeedStream> FetchFeedAsync(string? appKey, CancellationToken cancellationToken) =>
        FetchFeedAsync(cancellationToken);
}

public sealed record PhishTankFeedStream(string Name, Stream Content);
