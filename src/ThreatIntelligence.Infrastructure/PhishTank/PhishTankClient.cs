using System.Net;
using ThreatIntelligence.Application.Fetches;
using ThreatIntelligence.Infrastructure.Configuration;

namespace ThreatIntelligence.Infrastructure.PhishTank;

public sealed class PhishTankClient : IPhishTankClient
{
    public const string DefaultBaseUrl = "https://data.phishtank.com";
    private const string FeedFileName = "online-valid.json.gz";

    private readonly HttpClient _httpClient;
    private readonly string? _appKey;
    private readonly string _baseUrl;

    public PhishTankClient(HttpClient httpClient, PhishTankOptions options, string baseUrl = DefaultBaseUrl)
    {
        _httpClient = httpClient;
        _appKey = options.AppKey;
        _baseUrl = baseUrl;
        if (_httpClient.DefaultRequestHeaders.UserAgent.Count > 0) return;
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("ThreatIntelligence.Worker/1.0");
    }

    public async Task<PhishTankFeedStream> FetchFeedAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, BuildFeedUri());
        var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            throw new PhishTankQuotaException("PhishTank rate limit exceeded (HTTP 429).");
        }

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new PhishTankAuthenticationException(
                $"PhishTank authentication failed (HTTP {(int)response.StatusCode}).");
        }

        if (!response.IsSuccessStatusCode)
        {
            throw response.StatusCode >= HttpStatusCode.InternalServerError
                ? new ProviderFetchHttpException(response.StatusCode, "PhishTank feed request failed.")
                : new ProviderFetchHttpException(response.StatusCode, "PhishTank feed request was rejected.");
        }

        var content = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return new PhishTankFeedStream(FeedFileName, content);
    }

    internal Uri BuildFeedUri()
    {
        var baseUri = new Uri(_baseUrl.TrimEnd('/'));
        var path = string.IsNullOrEmpty(_appKey)
            ? $"{FeedFileName}"
            : $"{_appKey}/{FeedFileName}";
        return new Uri(baseUri, path);
    }
}

public interface IPhishTankClient
{
    Task<PhishTankFeedStream> FetchFeedAsync(CancellationToken cancellationToken);
}

public sealed record PhishTankFeedStream(string Name, Stream Content);
