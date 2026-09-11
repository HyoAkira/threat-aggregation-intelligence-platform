using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ThreatIntelligence.Application.BatchWriter;
using ThreatIntelligence.Application.Fetches;
using ThreatIntelligence.Application.ProviderRecords;
using ThreatIntelligence.Domain.Enums;
using ThreatIntelligence.Infrastructure.PhishTank;

namespace ThreatIntelligence.Infrastructure.PhishTank;

public sealed class PhishTankFetchExecutor(
    IPhishTankClient client,
    PhishTankFeedParser parser,
    ProviderRecordValidator validator,
    IIocBatchWriter batchWriter,
    ILogger<PhishTankFetchExecutor>? logger = null) : IProviderFetchExecutor
{
    private readonly ILogger<PhishTankFetchExecutor> _logger =
        logger ?? NullLogger<PhishTankFetchExecutor>.Instance;

    public async Task<FetchExecutionResult> ExecuteAsync(FetchJob job, CancellationToken cancellationToken)
    {
        if (job.ProviderId != PhishTankProviderValidator.PhishTankProviderId)
        {
            throw new TerminalFetchException(
                $"PhishTank executor only supports provider {PhishTankProviderValidator.PhishTankProviderId}, got {job.ProviderId}.");
        }

        if (job.Mode != FetchMode.Full)
        {
            throw new TerminalFetchException(
                $"PhishTank executor only supports Full mode, got {job.Mode}.");
        }

        _logger.LogInformation(
            "PhishTank fetch execution starting JobId={JobId} ProviderId={ProviderId} Mode={Mode}",
            job.JobId,
            job.ProviderId,
            job.Mode);

        PhishTankFeedStream feed;
        try
        {
            feed = await client.FetchFeedAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (PhishTankQuotaException ex)
        {
            _logger.LogError(ex, "PhishTank quota failure JobId={JobId}", job.JobId);
            throw new TerminalFetchException(ex.Message, ex);
        }
        catch (PhishTankAuthenticationException ex)
        {
            _logger.LogError(ex, "PhishTank authentication failure JobId={JobId}", job.JobId);
            throw new TerminalFetchException(ex.Message, ex);
        }

        await using var feedContent = feed.Content;

        var records = new List<NormalizedProviderRecord>();
        var recordIndex = 0;
        var accepted = 0;
        var rejected = 0;
        try
        {
            await foreach (var recordInput in parser.ParseAsync(feedContent, cancellationToken, skipMalformedRecords: true).ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();
                recordIndex++;

                var validation = validator.Validate(recordInput, recordIndex);
                if (!validation.IsValid)
                {
                    var failure = validation.Failure!;
                    _logger.LogWarning(
                        "PhishTank record skipped RecordIndex={RecordIndex} ProviderRecordId={ProviderRecordId} Field={Field} Code={Code} Message={Message}",
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
        catch (PhishTankRecordParseException ex)
        {
            _logger.LogError(
                ex,
                "PhishTank feed record parse failure RecordIndex={RecordIndex} Field={Field}",
                ex.RecordIndex,
                ex.Field);
            throw new TerminalFetchException($"PhishTank feed record parse failure at index {ex.RecordIndex}: {ex.Message}", ex);
        }

        _logger.LogInformation(
            "PhishTank feed parsed RecordsParsed={RecordsParsed} RecordsAccepted={RecordsAccepted} RecordsRejected={RecordsRejected}; applying batch",
            recordIndex,
            accepted,
            rejected);

        var result = await batchWriter.ApplyAsync(records, job.ProviderId, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "PhishTank fetch execution completed JobId={JobId} RecordsAdded={RecordsAdded} RecordsUpdated={RecordsUpdated}",
            job.JobId,
            result.Added,
            result.Updated);

        return new FetchExecutionResult(result.Added, result.Updated);
    }
}
