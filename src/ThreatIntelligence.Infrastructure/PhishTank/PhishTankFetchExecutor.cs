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
    IIocBatchWriter batchWriter) : IProviderFetchExecutor
{
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

        PhishTankFeedStream feed;
        try
        {
            feed = await client.FetchFeedAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (PhishTankQuotaException ex)
        {
            throw new TerminalFetchException(ex.Message, ex);
        }
        catch (PhishTankAuthenticationException ex)
        {
            throw new TerminalFetchException(ex.Message, ex);
        }

        await using var feedContent = feed.Content;

        var records = new List<NormalizedProviderRecord>();
        var recordIndex = 0;
        try
        {
            await foreach (var recordInput in parser.ParseAsync(feedContent, cancellationToken).ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();
                recordIndex++;

                var validation = validator.Validate(recordInput, recordIndex);
                if (!validation.IsValid)
                {
                    var failure = validation.Failure!;
                    throw new TerminalFetchException(
                        $"PhishTank record {failure.RecordIndex} rejected in field '{failure.Field}': {failure.Message}");
                }

                records.Add(validation.Normalized!);
            }
        }
        catch (PhishTankRecordParseException ex)
        {
            throw new TerminalFetchException($"PhishTank feed record parse failure at index {ex.RecordIndex}: {ex.Message}", ex);
        }

        var result = await batchWriter.ApplyAsync(records, job.ProviderId, cancellationToken).ConfigureAwait(false);
        return new FetchExecutionResult(result.Added, result.Updated);
    }
}
