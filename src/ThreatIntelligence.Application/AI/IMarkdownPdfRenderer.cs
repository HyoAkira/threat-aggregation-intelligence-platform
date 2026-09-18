namespace ThreatIntelligence.Application.AI;

public sealed record PdfReportMetadata(int IocId, DateTimeOffset GeneratedAtUtc);

public static class PdfReportLimits
{
    public const int MaxMarkdownCharacters = 100_000;
}

public interface IMarkdownPdfRenderer
{
    Task<byte[]> RenderAsync(
        string markdown,
        PdfReportMetadata metadata,
        CancellationToken cancellationToken = default);
}

public sealed class PdfRenderingException(string code, string message, Exception? innerException = null)
    : Exception(message, innerException)
{
    public string Code { get; } = code;
}
