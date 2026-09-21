using System.Net;
using ThreatIntelligence.Application.Fetches;

namespace ThreatIntelligence.UnitTests.Fetches;

public sealed class FetchErrorClassifierTests
{
    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public void Server_errors_are_transient(HttpStatusCode statusCode)
    {
        Assert.True(FetchErrorClassifier.IsTransient(new ProviderFetchHttpException(statusCode, "upstream failed")));
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.BadRequest)]
    public void Deterministic_http_errors_are_terminal(HttpStatusCode statusCode)
    {
        var result = FetchErrorClassifier.Classify(new ProviderFetchHttpException(statusCode, "request rejected"));

        Assert.IsType<TerminalFetchException>(result);
    }

    [Fact]
    public void Timeout_is_classified_as_transient()
    {
        var result = FetchErrorClassifier.Classify(new TimeoutException("timed out"));

        Assert.IsType<TransientFetchException>(result);
    }

    [Fact]
    public void Network_transport_failure_is_classified_as_transient()
    {
        var result = FetchErrorClassifier.Classify(new HttpRequestException("connection refused"));

        Assert.IsType<TransientFetchException>(result);
    }
}
