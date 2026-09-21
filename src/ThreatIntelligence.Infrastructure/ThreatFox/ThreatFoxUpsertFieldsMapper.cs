using ThreatIntelligence.Application.Abstractions.Hashing;
using ThreatIntelligence.Application.BatchWriter;
using ThreatIntelligence.Application.ProviderRecords;
using ThreatIntelligence.Domain.Entities;

namespace ThreatIntelligence.Infrastructure.ThreatFox;

/// <summary>
/// Maps a validated ThreatFox record into <see cref="IocUpsertFields"/> for the shared batch writer.
/// </summary>
public sealed class ThreatFoxUpsertFieldsMapper(IValueHasher hasher, TimeProvider timeProvider) : IIocUpsertFieldsMapper
{
    public IocUpsertFields Map(NormalizedProviderRecord record)
    {
        var metadata = record.Metadata;

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
            ThreatType: GetMetadataString(metadata, ThreatFoxRecordMapper.ThreatTypeKey),
            Malware: GetMetadataString(metadata, ThreatFoxRecordMapper.MalwareKey),
            MalwareAlias: GetMetadataString(metadata, ThreatFoxRecordMapper.MalwareAliasKey),
            MalwarePrintable: GetMetadataString(metadata, ThreatFoxRecordMapper.MalwarePrintableKey),
            IsCompromised: GetMetadataBool(metadata, ThreatFoxRecordMapper.IsCompromisedKey),
            Reporter: GetMetadataString(metadata, ThreatFoxRecordMapper.ReporterKey),
            Anonymous: null,
            IsVerified: null,
            Target: null,
            PhishTankDetails: null,
            PhishTankOnline: null,
            UrlHost: record.IocIdentity.UrlHost,
            ParentDomain: record.IocIdentity.ParentDomain,
            ThreatFoxConfidenceLevel: GetMetadataByte(metadata, ThreatFoxRecordMapper.ConfidenceLevelKey));
    }

    private static string? GetMetadataString(IReadOnlyDictionary<string, object?>? metadata, string key)
        => metadata is not null && metadata.TryGetValue(key, out var value) ? value as string : null;

    private static bool? GetMetadataBool(IReadOnlyDictionary<string, object?>? metadata, string key)
        => metadata is not null && metadata.TryGetValue(key, out var value) ? value as bool? : null;

    private static byte? GetMetadataByte(IReadOnlyDictionary<string, object?>? metadata, string key)
    {
        if (metadata is not null && metadata.TryGetValue(key, out var value) && value is int intValue)
        {
            return (byte)intValue;
        }

        return null;
    }
}
