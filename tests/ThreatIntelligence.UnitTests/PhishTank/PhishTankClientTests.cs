using System.Net;
using ThreatIntelligence.Application.Fetches;
using ThreatIntelligence.Infrastructure.PhishTank;

namespace ThreatIntelligence.UnitTests.PhishTank;

public sealed class PhishTankClientTests
{
    [Fact]
    public async Task Feed_request_uses_the_gzip_endpoint_without_key()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, "fake-gzip-data", "application/gzip");
        var client = PhishTankTestClient.Create(handler);

        var result = await client.FetchFeedAsync(CancellationToken.None);
        await using (result.Content)
        {
            Assert.Equal("online-valid.json.gz", result.Name);
        }

        var uri = handler.LastRequest!.RequestUri!.AbsoluteUri;
        Assert.StartsWith(PhishTankTestClient.BaseUrl, uri);
        Assert.Contains(PhishTankTestClient.FeedFileName, uri);
        Assert.DoesNotContain(PhishTankTestClient.AppKey, uri);
    }

    [Fact]
    public async Task Feed_request_includes_app_key_when_configured()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, "fake-gzip-data", "application/gzip");
        var client = PhishTankTestClient.Create(handler, appKey: PhishTankTestClient.AppKey);

        var result = await client.FetchFeedAsync(CancellationToken.None);
        await using (result.Content)
        {
            Assert.Equal("online-valid.json.gz", result.Name);
        }

        var uri = handler.LastRequest!.RequestUri!.AbsoluteUri;
        Assert.Contains($"{PhishTankTestClient.AppKey}/{PhishTankTestClient.FeedFileName}", uri);
    }

    [Fact]
    public async Task Response_body_is_returned_as_a_stream_for_incremental_consumption()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, "streamed payload", "application/gzip");
        var client = PhishTankTestClient.Create(handler);

        var result = await client.FetchFeedAsync(CancellationToken.None);
        using var reader = new StreamReader(result.Content);

        Assert.Equal("streamed payload", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task Caller_cancellation_propagates_as_cancellation_not_timeout()
    {
        var client = PhishTankTestClient.Create(new StubHttpMessageHandler((_, token) =>
        {
            token.ThrowIfCancellationRequested();
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("data") };
        }));

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.FetchFeedAsync(cts.Token));
    }

    [Fact]
    public async Task Request_carries_a_user_agent_that_identifies_the_application()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, "data");
        var client = PhishTankTestClient.Create(handler);

        await client.FetchFeedAsync(CancellationToken.None);

        Assert.Contains(
            handler.LastRequest!.Headers.UserAgent,
            product => product.Product is not null && !string.IsNullOrWhiteSpace(product.Product.Name));
    }

    [Fact]
    public async Task Rate_limit_failure_is_quota_exception_not_transport()
    {
        var client = PhishTankTestClient.Create(new StubHttpMessageHandler(HttpStatusCode.TooManyRequests));

        var ex = await Assert.ThrowsAsync<PhishTankQuotaException>(() =>
            client.FetchFeedAsync(CancellationToken.None));

        Assert.Contains("429", ex.Message);
        Assert.False(FetchErrorClassifier.IsTransient(ex));
    }

    [Fact]
    public async Task Unauthorized_is_authentication_failure()
    {
        var client = PhishTankTestClient.Create(new StubHttpMessageHandler(HttpStatusCode.Unauthorized));

        var ex = await Assert.ThrowsAsync<PhishTankAuthenticationException>(() =>
            client.FetchFeedAsync(CancellationToken.None));

        Assert.Contains("401", ex.Message);
        Assert.False(FetchErrorClassifier.IsTransient(ex));
    }

    [Fact]
    public async Task Forbidden_is_authentication_failure()
    {
        var client = PhishTankTestClient.Create(new StubHttpMessageHandler(HttpStatusCode.Forbidden));

        var ex = await Assert.ThrowsAsync<PhishTankAuthenticationException>(() =>
            client.FetchFeedAsync(CancellationToken.None));

        Assert.Contains("403", ex.Message);
        Assert.False(FetchErrorClassifier.IsTransient(ex));
    }

    [Fact]
    public async Task Server_failure_is_transport_failure_the_job_policy_retries()
    {
        var client = PhishTankTestClient.Create(new StubHttpMessageHandler(HttpStatusCode.BadGateway));

        var ex = await Assert.ThrowsAsync<ProviderFetchHttpException>(() =>
            client.FetchFeedAsync(CancellationToken.None));

        Assert.Equal(HttpStatusCode.BadGateway, ex.StatusCode);
        Assert.True(FetchErrorClassifier.IsTransient(ex));
    }

    [Fact]
    public async Task Client_error_is_terminal()
    {
        var client = PhishTankTestClient.Create(new StubHttpMessageHandler(HttpStatusCode.NotFound));

        var ex = await Assert.ThrowsAsync<ProviderFetchHttpException>(() =>
            client.FetchFeedAsync(CancellationToken.None));

        Assert.Equal(HttpStatusCode.NotFound, ex.StatusCode);
        Assert.False(FetchErrorClassifier.IsTransient(ex));
        var classified = FetchErrorClassifier.Classify(ex);
        Assert.Equal(typeof(TerminalFetchException), classified.GetType());
    }

    [Fact]
    public async Task Key_is_never_carried_in_a_request_header()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, "data");
        var client = PhishTankTestClient.Create(handler, appKey: PhishTankTestClient.AppKey);

        await client.FetchFeedAsync(CancellationToken.None);

        foreach (var header in handler.LastRequest!.Headers)
        {
            Assert.NotEqual("Authorization", header.Key, StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain(header.Value, value => value!.Contains(PhishTankTestClient.AppKey));
        }
    }
}
