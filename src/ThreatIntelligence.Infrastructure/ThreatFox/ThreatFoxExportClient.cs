using System.Net;
using ThreatIntelligence.Application.Fetches;
using ThreatIntelligence.Infrastructure.Configuration;

namespace ThreatIntelligence.Infrastructure.ThreatFox;

/// <summary>
/// Streams ThreatFox JSON exports. Recent mode maps to three per-type JSON files
/// (<c>domains_recent.json</c>, <c>urls_recent.json</c>, <c>ip_port_recent.json</c>);
/// full mode maps to <c>full.json.zip</c>. The API key lives only in the constructed URL for
/// the upstream request and is never placed into log or exception output.
/// </summary>
public sealed class ThreatFoxExportClient : IThreatFoxExportClient
{
    public const string DefaultBaseUrl = "https://threatfox-api.abuse.ch";

    private const string DomainsRecentFileName = "domains_recent.json";
    private const string UrlsRecentFileName = "urls_recent.json";
    private const string IpPortRecentFileName = "ip_port_recent.json";
    private const string FullExportFileName = "full.json.zip";

    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _baseUrl;

    public ThreatFoxExportClient(HttpClient httpClient, ThreatFoxOptions options, string baseUrl = DefaultBaseUrl)
    {
        _httpClient = httpClient;
        _apiKey = options.ApiKey;
        _baseUrl = baseUrl;

        if (_httpClient.DefaultRequestHeaders.UserAgent.Count > 0)
        {
            return;
        }

        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("ThreatIntelligence.Worker/1.0");
    }

    public Task<ThreatFoxExportStream> FetchRecentAsync(ThreatFoxRecentKind kind, CancellationToken cancellationToken)
    {
        var fileName = kind switch
        {
            ThreatFoxRecentKind.Domains => DomainsRecentFileName,
            ThreatFoxRecentKind.Urls => UrlsRecentFileName,
            ThreatFoxRecentKind.IpPort => IpPortRecentFileName,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };

        return FetchAsync(fileName, cancellationToken);
    }

    public Task<ThreatFoxExportStream> FetchFullAsync(CancellationToken cancellationToken)
        => FetchAsync(FullExportFileName, cancellationToken);

    /// <summary>The deterministic export URI for a file name; visible for tests.</summary>
    internal Uri BuildExportUri(string fileName)
        => new($"{_baseUrl.TrimEnd('/')}/v2/files/exports/{_apiKey}/{fileName}");

    private async Task<ThreatFoxExportStream> FetchAsync(string fileName, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, BuildExportUri(fileName));

        var response = await _httpClient
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            // 5xx/network failures classify as retryable transport failures via FetchErrorClassifier;
            // 429/401/403 and other 4xx classify as terminal. No URL or key in the message.
            throw response.StatusCode >= HttpStatusCode.InternalServerError
                ? new ProviderFetchHttpException(response.StatusCode, "ThreatFox export request failed.")
                : new ProviderFetchHttpException(response.StatusCode, "ThreatFox export request was rejected.");
        }

        var content = await response.Content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);

        return new ThreatFoxExportStream(fileName, content);
    }
}