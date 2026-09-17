using System.Data.Common;
using System.Net;
using System.Net.Sockets;
using ThreatIntelligence.Application.Abstractions.Secrets;

namespace ThreatIntelligence.Application.Fetches;

public sealed class TerminalFetchException(string message, Exception? innerException = null)
    : Exception(message, innerException);

public sealed class TransientFetchException(string message, Exception? innerException = null)
    : Exception(message, innerException);

public sealed class ProviderFetchHttpException(HttpStatusCode statusCode, string message)
    : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}

public static class FetchErrorClassifier
{
    public static bool IsTransient(Exception exception) => exception switch
    {
        TransientFetchException => true,
        HttpRequestException => true,
        SocketException => true,
        TimeoutException => true,
        DbException => true,
        ProviderFetchHttpException { StatusCode: >= HttpStatusCode.InternalServerError } => true,
        ProviderCredentialMissingException => false,
        _ => false
    };

    public static Exception Classify(Exception exception) =>
        exception is TerminalFetchException or TransientFetchException
            ? exception
            : IsTransient(exception)
                ? new TransientFetchException(exception.Message, exception)
                : new TerminalFetchException(exception.Message, exception);
}
