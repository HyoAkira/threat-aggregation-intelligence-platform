using ThreatIntelligence.Application.Abstractions.Hashing;
using ThreatIntelligence.Application.BatchWriter;
using ThreatIntelligence.Application.ProviderRecords;
using ThreatIntelligence.Domain.Entities;

namespace ThreatIntelligence.Infrastructure.PhishTank;

/// <summary>
/// Maps a validated PhishTank record into <see cref="IocUpsertFields"/> for the shared batch writer.
/// </summary>
public sealed class PhishTankUpsertFieldsMapper(IValueHasher hasher, TimeProvider timeProvider) : IIocUpsertFieldsMapper
{
    public IocUpsertFields Map(NormalizedProviderRecord record)
    {
        var metadata = record.Metadata;
        var payload = record.Payload;

        return new IocUpsertFields(
            ProviderRecordId: record.ProviderRecordId,
            IocValue: record.IocIdentity.IocValue,
            IocValueHash: hasher.Hash(record.IocIdentity.IocValue),
            IocType: record.IocIdentity.Type,
            Port: record.IocIdentity.Port,
            ProviderDetailsLink: record.ProviderDetailsLink,
            UpdatedAtUtc: timeProvider.GetUtcNow().UtcDateTime,
            FirstSeenUtc: record.FirstSeenUtc.UtcDateTime,
            LastSeenUtc: record.LastSeenUtc?.UtcDateTime,
            Tags: record.Tags,
            ThreatType: null,
            Malware: null,
            MalwareAlias: null,
            MalwarePrintable: null,
            IsCompromised: null,
            Reporter: null,
            Anonymous: null,
            IsVerified: metadata is not null && metadata.TryGetValue(PhishTankRecordMapper.VerifiedKey, out var v) ? v as bool? : null,
            Target: metadata is not null && metadata.TryGetValue(PhishTankRecordMapper.TargetKey, out var t) ? t as string : null,
            PhishTankDetails: payload is not null && payload.TryGetValue(PhishTankRecordMapper.PhishTankDetailsKey, out var d) ? d as string : null,
            PhishTankOnline: metadata is not null && metadata.TryGetValue(PhishTankRecordMapper.OnlineKey, out var o) ? o as bool? : null,
            UrlHost: record.IocIdentity.UrlHost,
            ParentDomain: record.IocIdentity.ParentDomain,
            ThreatFoxConfidenceLevel: null);
    }
}
