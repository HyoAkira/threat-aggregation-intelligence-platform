using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ThreatIntelligence.Application.BatchWriter;
using ThreatIntelligence.Application.Fetches;
using ThreatIntelligence.Application.ProviderRecords;
using ThreatIntelligence.Domain.Enums;

namespace ThreatIntelligence.Infrastructure.ThreatFox;

public sealed class ThreatFoxFetchExecutor(
    IThreatFoxExportClient client,
    ThreatFoxRecentParser recentParser,
    ThreatFoxFullParser fullParser,
    ProviderRecordValidator validator,
    IIocBatchWriter batchWriter,
    ILogger<ThreatFoxFetchExecutor>? logger = null) : IProviderFetchExecutor
{
    private readonly ILogger<ThreatFoxFetchExecutor> _logger =
        logger ?? NullLogger<ThreatFoxFetchExecutor>.Instance;

    public async Task<FetchExecutionResult> ExecuteAsync(FetchJob job, CancellationToken cancellationToken)
    {
        if (job.ProviderId != ThreatFoxRecordMapper.ThreatFoxProviderId)
        {
            throw new TerminalFetchException(
                $"ThreatFox executor only supports provider {ThreatFoxRecordMapper.ThreatFoxProviderId}, got {job.ProviderId}.");
        }

        _logger.LogInformation(
            "ThreatFox fetch execution starting JobId={JobId} ProviderId={ProviderId} Mode={Mode}",
            job.JobId,
            job.ProviderId,
            job.Mode);

        var records = job.Mode switch
        {
            FetchMode.Recent => await FetchRecentAsync(cancellationToken).ConfigureAwait(false),
            FetchMode.Full => await FetchFullAsync(cancellationToken).ConfigureAwait(false),
            _ => throw new TerminalFetchException($"ThreatFox executor does not support mode {job.Mode}.")
        };

        _logger.LogInformation(
            "ThreatFox parsed and validated {RecordCount} record(s) JobId={JobId} Mode={Mode}; applying batch",
            records.Count,
            job.JobId,
            job.Mode);

        var result = await batchWriter.ApplyAsync(records, job.ProviderId, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "ThreatFox fetch execution completed JobId={JobId} Mode={Mode} RecordsAdded={RecordsAdded} RecordsUpdated={RecordsUpdated}",
            job.JobId,
            job.Mode,
            result.Added,
            result.Updated);

        return new FetchExecutionResult(result.Added, result.Updated);
    }

    private async Task<List<NormalizedProviderRecord>> FetchRecentAsync(CancellationToken ct)
    {
        _logger.LogInformation("Fetching ThreatFox recent export");

        ThreatFoxExportStream stream;
        try
        {
            stream = await client.FetchRecentAsync(ct).ConfigureAwait(false);
        }
        catch (ProviderFetchHttpException ex)
        {
            _logger.LogError(
                ex,
                "ThreatFox recent export request failed StatusCode={StatusCode}",
                (int)ex.StatusCode);
            throw new TerminalFetchException("ThreatFox recent export request failed.", ex);
        }

        var records = new List<NormalizedProviderRecord>();
        try
        {
            await ParseAndValidateAsync(recentParser.ParseAsync(stream.Content, ct), records, "Recent", ct)
                .ConfigureAwait(false);
        }
        finally
        {
            await stream.Content.DisposeAsync().ConfigureAwait(false);
        }

        return records;
    }

    private async Task<List<NormalizedProviderRecord>> FetchFullAsync(CancellationToken ct)
    {
        ThreatFoxExportStream stream;
        try
        {
            stream = await client.FetchFullAsync(ct).ConfigureAwait(false);
        }
        catch (ProviderFetchHttpException ex)
        {
            _logger.LogError(
                ex,
                "ThreatFox full export request failed StatusCode={StatusCode}",
                (int)ex.StatusCode);
            throw new TerminalFetchException("ThreatFox full export request failed.", ex);
        }

        var records = new List<NormalizedProviderRecord>();
        try
        {
            await ParseAndValidateAsync(fullParser.ParseAsync(stream.Content, ct), records, "Full", ct)
                .ConfigureAwait(false);
        }
        finally
        {
            await stream.Content.DisposeAsync().ConfigureAwait(false);
        }

        return records;
    }

    private async Task ParseAndValidateAsync(
        IAsyncEnumerable<ProviderRecordInput> source,
        List<NormalizedProviderRecord> records,
        string sourceLabel,
        CancellationToken ct)
    {
        var recordIndex = 0;
        var accepted = 0;
        var rejected = 0;
        try
        {
            await foreach (var recordInput in source.ConfigureAwait(false))
            {
                ct.ThrowIfCancellationRequested();
                recordIndex++;

                var validation = validator.Validate(recordInput, recordIndex);
                if (!validation.IsValid)
                {
                    var failure = validation.Failure!;
                    _logger.LogWarning(
                        "ThreatFox record skipped Source={Source} RecordIndex={RecordIndex} ProviderRecordId={ProviderRecordId} Field={Field} Code={Code} Message={Message}",
                        sourceLabel,
                        failure.RecordIndex,
                        recordInput.ProviderRecordId,
                        failure.Field,
                        failure.Code,
                        failure.Message);
                    rejected++;
                    continue;
                }

                records.Add(validation.Normalized!);
                accepted++;
            }
        }
        catch (ThreatFoxRecordParseException ex)
        {
            _logger.LogError(
                ex,
                "ThreatFox feed record parse failure Source={Source} RecordIndex={RecordIndex} Field={Field} Reason={Reason}",
                sourceLabel,
                ex.RecordIndex,
                ex.Field,
                ex.Reason);
            throw new TerminalFetchException(
                $"ThreatFox feed record parse failure at index {ex.RecordIndex}: {ex.Message}", ex);
        }
        catch (ThreatFoxFeedFormatException ex)
        {
            _logger.LogError(
                ex,
                "ThreatFox feed format failure Source={Source} PayloadPrefix={PayloadPrefix}",
                sourceLabel,
                ex.PayloadPrefix);
            throw new TerminalFetchException($"ThreatFox feed format failure: {ex.Message}", ex);
        }
        catch (ThreatFoxArchiveException ex)
        {
            _logger.LogError(ex, "ThreatFox archive failure Source={Source}", sourceLabel);
            throw new TerminalFetchException($"ThreatFox archive failure: {ex.Message}", ex);
        }

        _logger.LogInformation(
            "ThreatFox source processed Source={Source} RecordsParsed={RecordsParsed} RecordsAccepted={RecordsAccepted} RecordsRejected={RecordsRejected}",
            sourceLabel,
            recordIndex,
            accepted,
            rejected);
    }
}
