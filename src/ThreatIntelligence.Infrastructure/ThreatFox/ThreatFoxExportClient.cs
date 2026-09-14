using System.Diagnostics;
using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ThreatIntelligence.Application.Fetches;
using ThreatIntelligence.Infrastructure.Configuration;

namespace ThreatIntelligence.Infrastructure.ThreatFox;

/// <summary>
/// Streams ThreatFox JSON exports. Recent mode maps to the combined <c>recent.json</c> file;
/// full mode maps to <c>full.json.zip</c>. The API key lives only in the constructed URL for
/// the upstream request and is never placed into log or exception output.
/// </summary>
public sealed class ThreatFoxExportClient : IThreatFoxExportClient
{
    public const string DefaultBaseUrl = "https://threatfox-api.abuse.ch";

    private const string RecentExportFileName = "recent.json";
    private const string FullExportFileName = "full.json.zip";

    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _baseUrl;
    private readonly ILogger<ThreatFoxExportClient> _logger;

    public ThreatFoxExportClient(
        HttpClient httpClient,
        ThreatFoxOptions options,
        string baseUrl = DefaultBaseUrl,
        ILogger<ThreatFoxExportClient>? logger = null)
    {
        _httpClient = httpClient;
        _apiKey = options.ApiKey;
        _baseUrl = baseUrl;
        _logger = logger ?? NullLogger<ThreatFoxExportClient>.Instance;

        if (_httpClient.DefaultRequestHeaders.UserAgent.Count > 0)
        {
            return;
        }

        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("ThreatIntelligence.Worker/1.0");
    }

    public Task<ThreatFoxExportStream> FetchRecentAsync(CancellationToken cancellationToken)
        => FetchAsync(RecentExportFileName, cancellationToken);

    public Task<ThreatFoxExportStream> FetchFullAsync(CancellationToken cancellationToken)
        => FetchAsync(FullExportFileName, cancellationToken);

    /// <summary>The deterministic export URI for a file name; visible for tests.</summary>
    internal Uri BuildExportUri(string fileName)
        => new($"{_baseUrl.TrimEnd('/')}/v2/files/exports/{_apiKey}/{fileName}");

    private async Task<ThreatFoxExportStream> FetchAsync(string fileName, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, BuildExportUri(fileName));

        _logger.LogInformation("ThreatFox export request starting FileName={FileName}", fileName);

        var stopwatch = Stopwatch.StartNew();
        var response = await _httpClient
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        stopwatch.Stop();

        if (!response.IsSuccessStatusCode)
        {
            var payloadPrefix = await ReadDiagnosticPrefixAsync(response, cancellationToken).ConfigureAwait(false);
            _logger.LogError(
                "ThreatFox export request failed FileName={FileName} StatusCode={StatusCode} ElapsedMs={ElapsedMs} PayloadPrefix={PayloadPrefix}",
                fileName,
                (int)response.StatusCode,
                stopwatch.ElapsedMilliseconds,
                payloadPrefix);

            // 5xx/network failures classify as retryable transport failures via FetchErrorClassifier;
            // 429/401/403 and other 4xx classify as terminal. No URL or key in the message.
            throw response.StatusCode >= HttpStatusCode.InternalServerError
                ? new ProviderFetchHttpException(response.StatusCode, "ThreatFox export request failed.")
                : new ProviderFetchHttpException(response.StatusCode, "ThreatFox export request was rejected.");
        }

        _logger.LogInformation(
            "ThreatFox export response received FileName={FileName} StatusCode={StatusCode} ContentType={ContentType} ContentLength={ContentLength} ElapsedMs={ElapsedMs}",
            fileName,
            (int)response.StatusCode,
            response.Content.Headers.ContentType?.ToString(),
            response.Content.Headers.ContentLength,
            stopwatch.ElapsedMilliseconds);

        var content = await response.Content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);

        return new ThreatFoxExportStream(fileName, content);
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