using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using ThreatIntelligence.Application.AI;

namespace ThreatIntelligence.Infrastructure.Reporting;

public sealed class MarkdownPdfRenderer(
    string pandocPath,
    string templatePath,
    ILogger<MarkdownPdfRenderer> logger) : IMarkdownPdfRenderer
{
    private const long MaxPdfBytes = 10 * 1024 * 1024;

    public async Task<byte[]> RenderAsync(
        string markdown,
        PdfReportMetadata metadata,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            throw new PdfRenderingException("pdf_input_invalid", "The report is empty.");
        }

        if (markdown.Length > PdfReportLimits.MaxMarkdownCharacters)
        {
            throw new PdfRenderingException(
                "pdf_input_too_large",
                "The report exceeds the PDF input size limit.");
        }

        // Reports are text-only. Reject image syntax so a caller cannot make pandoc read an
        // arbitrary server-local file while converting the document.
        if (Regex.IsMatch(markdown, @"!\s*\["))
        {
            throw new PdfRenderingException(
                "pdf_input_invalid",
                "Image content is not supported in PDF exports.");
        }

        if (!File.Exists(templatePath))
        {
            throw new PdfRenderingException(
                "pdf_template_missing",
                "The PDF template is not available.");
        }

        using var renderTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        renderTimeout.CancelAfter(TimeSpan.FromSeconds(60));
        var renderCancellationToken = renderTimeout.Token;

        var workDirectory = Path.Combine(
            Path.GetTempPath(),
            "threat-intelligence-pdf",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDirectory);

        var markdownPath = Path.Combine(workDirectory, "report.md");
        var outputPath = Path.Combine(workDirectory, "report.pdf");

        try
        {
            await File.WriteAllTextAsync(
                markdownPath,
                markdown,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                renderCancellationToken);

            var processStartInfo = new ProcessStartInfo
            {
                FileName = pandocPath,
                WorkingDirectory = workDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
            };
            processStartInfo.ArgumentList.Add(markdownPath);
            processStartInfo.ArgumentList.Add("--from=gfm-raw_html");
            processStartInfo.ArgumentList.Add("--standalone");
            processStartInfo.ArgumentList.Add("--template");
            processStartInfo.ArgumentList.Add(templatePath);
            processStartInfo.ArgumentList.Add("--pdf-engine=xelatex");
            processStartInfo.ArgumentList.Add("--pdf-engine-opt=--no-shell-escape");
            processStartInfo.ArgumentList.Add("--number-sections");
            processStartInfo.ArgumentList.Add("--metadata");
            processStartInfo.ArgumentList.Add("title:IOC Threat Intelligence Report");
            processStartInfo.ArgumentList.Add("--metadata");
            processStartInfo.ArgumentList.Add($"subtitle:Indicator of Compromise {metadata.IocId}");
            processStartInfo.ArgumentList.Add("--metadata");
            processStartInfo.ArgumentList.Add(
                $"date:{metadata.GeneratedAtUtc.ToUniversalTime():yyyy-MM-dd HH:mm} UTC");
            processStartInfo.ArgumentList.Add("--output");
            processStartInfo.ArgumentList.Add(outputPath);

            using var process = new Process { StartInfo = processStartInfo };
            try
            {
                if (!process.Start())
                {
                    throw new PdfRenderingException(
                        "pdf_renderer_unavailable",
                        "The PDF renderer could not be started.");
                }
            }
            catch (Win32Exception exception)
            {
                throw new PdfRenderingException(
                    "pdf_renderer_unavailable",
                    "Pandoc is not installed or cannot be started.",
                    exception);
            }

            var standardErrorTask = process.StandardError.ReadToEndAsync(renderCancellationToken);
            try
            {
                await process.WaitForExitAsync(renderCancellationToken);
            }
            catch (OperationCanceledException) when (renderTimeout.IsCancellationRequested)
            {
                TryKill(process);
                if (!cancellationToken.IsCancellationRequested)
                {
                    throw new PdfRenderingException(
                        "pdf_render_timeout",
                        "The PDF conversion exceeded its time limit.");
                }

                throw;
            }

            var standardError = await standardErrorTask;
            if (process.ExitCode != 0)
            {
                logger.LogWarning(
                    "Pandoc failed to render IOC report with exit code {ExitCode}; stderr length {ErrorLength}",
                    process.ExitCode,
                    standardError.Length);
                throw new PdfRenderingException(
                    "pdf_render_failed",
                    "Pandoc could not render the report.");
            }

            var fileInfo = new FileInfo(outputPath);
            if (!fileInfo.Exists || fileInfo.Length == 0 || fileInfo.Length > MaxPdfBytes)
            {
                throw new PdfRenderingException(
                    "pdf_render_failed",
                    "Pandoc did not produce a valid PDF.");
            }

            return await File.ReadAllBytesAsync(outputPath, renderCancellationToken);
        }
        finally
        {
            TryDeleteDirectory(workDirectory);
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // The process may have exited between HasExited and Kill.
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
            // Temporary cleanup must not mask the render result.
        }
        catch (UnauthorizedAccessException)
        {
            // Temporary cleanup must not mask the render result.
        }
    }
}
