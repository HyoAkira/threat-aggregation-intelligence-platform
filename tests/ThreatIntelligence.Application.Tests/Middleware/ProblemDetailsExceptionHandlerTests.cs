using System.Text.Json;
using Microsoft.AspNetCore.Http;
using ThreatIntelligence.API.Middleware;

namespace ThreatIntelligence.Application.Tests.Middleware;

public class ProblemDetailsExceptionHandlerTests
{
    private static async Task<(bool Handled, int StatusCode, string? ContentType, JsonElement Body)> InvokeAsync(Exception exception)
    {
        var handler = new ProblemDetailsExceptionHandler();
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/threatfox/download";
        context.Response.Body = new MemoryStream();

        var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var bodyText = await reader.ReadToEndAsync();
        var body = JsonDocument.Parse(bodyText).RootElement.Clone();

        return (handled, context.Response.StatusCode, context.Response.ContentType, body);
    }

    [Fact]
    public async Task MissingConfiguration_MapsTo401()
    {
        var exception = new InvalidOperationException("ThreatFox API key (AuthKey:AbuseCh) is not configured.");

        var result = await InvokeAsync(exception);

        Assert.True(result.Handled);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
        Assert.Equal("application/problem+json", result.ContentType);
        Assert.Equal(401, result.Body.GetProperty("status").GetInt32());
        Assert.Equal("Configuration missing", result.Body.GetProperty("title").GetString());
        Assert.Contains("not configured", result.Body.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task HttpRequestException_MapsTo502()
    {
        var exception = new HttpRequestException("ThreatFox API returned 500: boom");

        var result = await InvokeAsync(exception);

        Assert.True(result.Handled);
        Assert.Equal(StatusCodes.Status502BadGateway, result.StatusCode);
        Assert.Equal(502, result.Body.GetProperty("status").GetInt32());
        Assert.Equal("Upstream service error", result.Body.GetProperty("title").GetString());
    }

    [Fact]
    public async Task InvalidDataException_MapsTo422()
    {
        var exception = new InvalidDataException("ThreatFox API response contained no IOC entries.");

        var result = await InvokeAsync(exception);

        Assert.True(result.Handled);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, result.StatusCode);
        Assert.Equal(422, result.Body.GetProperty("status").GetInt32());
        Assert.Equal("Invalid data", result.Body.GetProperty("title").GetString());
    }

    [Fact]
    public async Task UnknownException_MapsTo500_WithGenericMessage()
    {
        var exception = new Exception("secret internal detail");

        var result = await InvokeAsync(exception);

        Assert.True(result.Handled);
        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);
        Assert.Equal(500, result.Body.GetProperty("status").GetInt32());
        Assert.Equal("Internal server error", result.Body.GetProperty("title").GetString());
        Assert.DoesNotContain("secret internal detail", result.Body.GetProperty("detail").GetString());
    }
}