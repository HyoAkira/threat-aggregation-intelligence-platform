using System.Net;

namespace ThreatIntelligence.UnitTests.PhishTank;

internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> _respond;

    public StubHttpMessageHandler(HttpStatusCode statusCode, string content = "", string? contentType = null)
        : this((_, _) => CreateResponse(statusCode, content, contentType))
    {
    }

    public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        : this((request, _) => respond(request))
    {
    }

    public StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> respond)
    {
        _respond = respond;
    }

    public HttpRequestMessage? LastRequest { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        return Task.FromResult(_respond(request, cancellationToken));
    }

    private static HttpResponseMessage CreateResponse(HttpStatusCode statusCode, string content, string? contentType)
    {
        var response = new HttpResponseMessage(statusCode) { Content = new StringContent(content) };
        if (contentType is not null)
        {
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        }

        return response;
    }
}

internal static class PhishTankTestClient
{
    public const string AppKey = "phishtank-test-secret";
    public const string BaseUrl = "https://data.phishtank.com";
    public const string FeedFileName = "online-valid.json.gz";

    public static Infrastructure.PhishTank.PhishTankClient Create(StubHttpMessageHandler handler, string? appKey = null)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = null };
        return new Infrastructure.PhishTank.PhishTankClient(
            httpClient,
            new Infrastructure.Configuration.PhishTankOptions(appKey),
            BaseUrl);
    }
}
