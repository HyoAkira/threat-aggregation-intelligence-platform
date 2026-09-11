using System.Net;
using ThreatIntelligence.Application.Fetches;

namespace ThreatIntelligence.UnitTests.ThreatFox;

public sealed class ThreatFoxExportClientTests
{
    [Theory]
    [InlineData(ThreatFoxRecentKind.Domains, "domains_recent.json")]
    [InlineData(ThreatFoxRecentKind.Urls, "urls_recent.json")]
    [InlineData(ThreatFoxRecentKind.IpPort, "ip_port_recent.json")]
    public async Task Recent_requests_use_the_per_type_endpoint(ThreatFoxRecentKind kind, string expectedFileName)
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, "[]", "application/json");
        var client = ThreatFoxTestClient.Create(handler);

        var result = await client.FetchRecentAsync(kind, CancellationToken.None);
        await using (result.Content)
        {
            Assert.Equal(expectedFileName, result.Name);
        }

        var uri = handler.LastRequest!.RequestUri!.AbsoluteUri;
        Assert.StartsWith(ThreatFoxTestClient.BaseUrl, uri);
        Assert.Contains($"{ThreatFoxTestClient.ExpectedExportPath}{ThreatFoxTestClient.ApiKey}/{expectedFileName}", uri);
    }

    [Fact]
    public async Task Full_request_uses_the_zip_endpoint()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, "PK");
        var client = ThreatFoxTestClient.Create(handler);

        var result = await client.FetchFullAsync(CancellationToken.None);
        await using (result.Content)
        {
            Assert.Equal("full.json.zip", result.Name);
        }

        Assert.Contains($"{ThreatFoxTestClient.ExpectedExportPath}{ThreatFoxTestClient.ApiKey}/full.json.zip", handler.LastRequest!.RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task Response_body_is_returned_as_a_stream_for_incremental_consumption()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, "streamed payload", "application/json");
        var client = ThreatFoxTestClient.Create(handler);

        var result = await client.FetchRecentAsync(ThreatFoxRecentKind.Domains, CancellationToken.None);
        using var reader = new StreamReader(result.Content);

        Assert.Equal("streamed payload", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task Caller_cancellation_propagates_as_cancellation_not_timeout()
    {
        var client = ThreatFoxTestClient.Create(new StubHttpMessageHandler((_, token) =>
        {
            token.ThrowIfCancellationRequested();
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]") };
        }));

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.FetchRecentAsync(ThreatFoxRecentKind.Domains, cts.Token));
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task Rejection_failure_messages_never_contain_the_key_or_credential_bearing_url(HttpStatusCode statusCode)
    {
        var client = ThreatFoxTestClient.Create(new StubHttpMessageHandler(statusCode));

        var ex = await Assert.ThrowsAsync<ProviderFetchHttpException>(() =>
            client.FetchRecentAsync(ThreatFoxRecentKind.Domains, CancellationToken.None));

        Assert.Equal(statusCode, ex.StatusCode);
        Assert.DoesNotContain(ThreatFoxTestClient.ApiKey, ex.Message);
        Assert.DoesNotContain(ThreatFoxTestClient.ExpectedExportPath, ex.Message);
        Assert.DoesNotContain("threatfox-test-secret", ex.Message);
    }

    [Fact]
    public async Task Key_is_never_carried_in_a_request_header()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, "[]");
        var client = ThreatFoxTestClient.Create(handler);

        await client.FetchRecentAsync(ThreatFoxRecentKind.Domains, CancellationToken.None);

        foreach (var header in handler.LastRequest!.Headers)
        {
            Assert.NotEqual("Authorization", header.Key, StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain(header.Value, value => value!.Contains("threatfox-test-secret"));
        }
    }

    [Fact]
    public async Task Request_carries_a_user_agent_that_identifies_the_application()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, "[]");
        var client = ThreatFoxTestClient.Create(handler);

        await client.FetchRecentAsync(ThreatFoxRecentKind.Domains, CancellationToken.None);

        Assert.Contains(
            handler.LastRequest!.Headers.UserAgent,
            product => product.Product is not null && !string.IsNullOrWhiteSpace(product.Product.Name));
    }

    [Fact]
    public async Task Server_failures_surface_as_provider_http_failures_the_job_policy_retries()
    {
        var client = ThreatFoxTestClient.Create(new StubHttpMessageHandler(HttpStatusCode.BadGateway));

        var ex = await Assert.ThrowsAsync<ProviderFetchHttpException>(() =>
            client.FetchRecentAsync(ThreatFoxRecentKind.Domains, CancellationToken.None));

        Assert.Equal(HttpStatusCode.BadGateway, ex.StatusCode);
        Assert.True(FetchErrorClassifier.IsTransient(ex));
    }

    [Fact]
    public async Task Quota_failures_surface_as_terminal_to_the_job_policy()
    {
        var client = ThreatFoxTestClient.Create(new StubHttpMessageHandler(HttpStatusCode.TooManyRequests));

        var ex = await Assert.ThrowsAsync<ProviderFetchHttpException>(() =>
            client.FetchRecentAsync(ThreatFoxRecentKind.Domains, CancellationToken.None));

        Assert.Equal(HttpStatusCode.TooManyRequests, ex.StatusCode);
        var classified = FetchErrorClassifier.Classify(ex);
        Assert.False(FetchErrorClassifier.IsTransient(ex));
        Assert.Equal(typeof(TerminalFetchException), classified.GetType());
    }
}