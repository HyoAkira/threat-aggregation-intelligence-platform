using System.Data;
using Microsoft.EntityFrameworkCore;
using ThreatIntelligence.Application.Abstractions.Hashing;
using ThreatIntelligence.Application.Fetches;
using ThreatIntelligence.Application.ProviderRecords;
using ThreatIntelligence.Domain.Enums;
using ThreatIntelligence.Infrastructure.Persistence;
using ThreatIntelligence.Infrastructure.PhishTank;

namespace ThreatIntelligence.Infrastructure.PhishTank;

public sealed class PhishTankFetchExecutor(
    IPhishTankClient client,
    PhishTankFeedParser parser,
    ProviderRecordValidator validator,
    IValueHasher hasher,
    IDbContextFactory<ThreatIntelligenceDbContext> contextFactory) : IProviderFetchExecutor
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
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken).ConfigureAwait(false);

        var existingRecordIds = await context.Iocs
            .AsNoTracking()
            .Where(i => i.ProviderId == PhishTankProviderValidator.PhishTankProviderId)
            .Select(i => i.ProviderRecordId)
            .ToHashSetAsync(cancellationToken)
            .ConfigureAwait(false);

        var added = 0;
        var updated = 0;

        try
        {
            var recordIndex = 0;
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

                var normalized = validation.Normalized!;
                var now = DateTime.UtcNow;
                var iocValue = normalized.IocIdentity.IocValue;
                var iocValueHash = hasher.Hash(iocValue);

                if (existingRecordIds.Contains(normalized.ProviderRecordId))
                {
                    updated++;
                    continue;
                }

                var ioc = new Domain.Entities.IoC(
                    providerId: normalized.ProviderId,
                    providerRecordId: normalized.ProviderRecordId,
                    iocValue: iocValue,
                    iocValueHash: iocValueHash,
                    iocType: normalized.IocIdentity.Type,
                    firstSeenUtc: normalized.FirstSeenUtc.UtcDateTime,
                    createdAt: now,
                    updatedAt: now,
                    providerDetailsLink: normalized.ProviderDetailsLink,
                    port: normalized.IocIdentity.Port,
                    lastSeenUtc: normalized.LastSeenUtc?.UtcDateTime,
                    isVerified: normalized.Metadata?.TryGetValue(PhishTankRecordMapper.VerifiedKey, out var v) == true ? v as bool? : null,
                    target: normalized.Metadata?.TryGetValue(PhishTankRecordMapper.TargetKey, out var t) == true ? t as string : null,
                    phishTankDetails: normalized.Payload?.TryGetValue(PhishTankRecordMapper.PhishTankDetailsKey, out var d) == true ? d as string : null,
                    phishTankOnline: normalized.Metadata?.TryGetValue(PhishTankRecordMapper.OnlineKey, out var o) == true ? o as bool? : null,
                    urlHost: normalized.IocIdentity.UrlHost,
                    parentDomain: normalized.IocIdentity.ParentDomain);

                context.Iocs.Add(ioc);
                existingRecordIds.Add(normalized.ProviderRecordId);
                added++;
            }

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (TerminalFetchException)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
        catch (PhishTankRecordParseException ex)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw new TerminalFetchException($"PhishTank feed record parse failure at index {ex.RecordIndex}: {ex.Message}", ex);
        }
        catch (Exception)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }

        return new FetchExecutionResult(added, updated);
    }
}
