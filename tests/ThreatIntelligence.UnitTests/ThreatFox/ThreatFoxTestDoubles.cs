using System.Net;

namespace ThreatIntelligence.UnitTests.ThreatFox;

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

internal static class ThreatFoxTestClient
{
    public const string ApiKey = "threatfox-test-secret";
    public const string BaseUrl = "https://threatfox-api.abuse.ch";
    public const string ExpectedExportPath = "/v2/files/exports/";

    public static Infrastructure.ThreatFox.ThreatFoxExportClient Create(StubHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = null };
        return new Infrastructure.ThreatFox.ThreatFoxExportClient(
            httpClient,
            new Infrastructure.Configuration.ThreatFoxOptions(ApiKey),
            BaseUrl);
    }
}