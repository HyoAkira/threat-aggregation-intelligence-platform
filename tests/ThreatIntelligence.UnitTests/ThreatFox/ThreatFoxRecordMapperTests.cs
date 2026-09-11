using ThreatIntelligence.Application.ProviderRecords;
using ThreatIntelligence.Domain.Enums;
using ThreatIntelligence.Infrastructure.ThreatFox;

namespace ThreatIntelligence.UnitTests.ThreatFox;

public sealed class ThreatFoxRecordMapperTests
{
    private readonly ThreatFoxRecordMapper _mapper = new();

    private static ThreatFoxJsonRecord FullRecord() => new()
    {
        Id = "1020675",
        Ioc = "http://116.202.5.101/1679",
        IocType = "url",
        IocTypeDesc = "URL that is used for botnet Command&control (C&C)",
        ThreatType = "botnet_cc",
        ThreatTypeDesc = "Indicator that identifies a botnet command&control server (C&C)",
        Malware = "win.vidar",
        MalwarePrintable = "Vidar",
        MalwareAlias = "Katana",
        MalwareMalpedia = "https://malpedia.caad.fkie.fraunhofer.de/details/win.vidar",
        ConfidenceLevel = 100,
        FirstSeen = "2022-11-18 07:48:30 UTC",
        LastSeen = null,
        Reference = "https://twitter.com/example/status/123",
        Reporter = "crep1x",
        IsCompromised = false,
        Tags = ["Vidar", " vidar ", "BOTNET"],
    };

    [Fact]
    public void Every_documented_field_is_mapped_to_its_canonical_location()
    {
        var input = _mapper.Map(FullRecord(), recordIndex: 1);

        Assert.Equal(ThreatFoxRecordMapper.ThreatFoxProviderId, input.ProviderId);
        Assert.Equal("1020675", input.ProviderRecordId);
        Assert.Equal("http://116.202.5.101/1679", input.RawIoc);
        Assert.Equal(IocType.Url, input.DeclaredType);
        Assert.Equal(new DateTime(2022, 11, 18, 7, 48, 30, DateTimeKind.Utc), input.FirstSeenAt);
        Assert.Null(input.LastSeenAt);
        Assert.Equal("https://threatfox.abuse.ch/ioc/1020675", input.ProviderDetailsLink);

        var metadata = input.Metadata!;
        Assert.Equal("botnet_cc", metadata[ThreatFoxRecordMapper.ThreatTypeKey]);
        Assert.Equal(
            "Indicator that identifies a botnet command&control server (C&C)",
            metadata[ThreatFoxRecordMapper.ThreatTypeDescKey]);
        Assert.Equal("url", metadata[ThreatFoxRecordMapper.IocTypeKey]);
        Assert.Equal("URL that is used for botnet Command&control (C&C)", metadata[ThreatFoxRecordMapper.IocTypeDescKey]);
        Assert.Equal("win.vidar", metadata[ThreatFoxRecordMapper.MalwareKey]);
        Assert.Equal("Vidar", metadata[ThreatFoxRecordMapper.MalwarePrintableKey]);
        Assert.Equal("Katana", metadata[ThreatFoxRecordMapper.MalwareAliasKey]);
        Assert.Equal("https://malpedia.caad.fkie.fraunhofer.de/details/win.vidar", metadata[ThreatFoxRecordMapper.MalwareMalpediaKey]);
        Assert.Equal(100, metadata[ThreatFoxRecordMapper.ConfidenceLevelKey]);
        Assert.False((bool)metadata[ThreatFoxRecordMapper.IsCompromisedKey]!);
        Assert.Equal("crep1x", metadata[ThreatFoxRecordMapper.ReporterKey]);
        Assert.Equal("https://twitter.com/example/status/123", metadata[ThreatFoxRecordMapper.ReferenceKey]);
    }

    [Fact]
    public void Last_seen_and_optional_metadata_are_retained_when_present()
    {
        var record = FullRecord() with
        {
            LastSeen = "2022-11-19 00:00:00 UTC",
            Reporter = "",
        };

        var input = _mapper.Map(record, 1);

        Assert.Equal(new DateTime(2022, 11, 19, 0, 0, 0, DateTimeKind.Utc), input.LastSeenAt);
        Assert.Equal("", input.Metadata![ThreatFoxRecordMapper.ReporterKey]);
    }

    [Fact]
    public void Tags_are_trimmed_lowercased_and_de_duplicated()
    {
        var input = _mapper.Map(FullRecord(), 1);

        Assert.Equal(["vidar", "botnet"], input.Tags);
    }

    [Fact]
    public void No_tags_leave_the_tag_collection_null()
    {
        var record = FullRecord() with { Tags = null };

        var input = _mapper.Map(record, 1);

        Assert.Null(input.Tags);
    }

    [Fact]
    public void False_boolean_is_retained_and_distinct_from_absent()
    {
        var withFalse = _mapper.Map(FullRecord(), 1);
        var withAbsent = _mapper.Map(FullRecord() with { IsCompromised = null }, 1);

        Assert.False((bool)withFalse.Metadata![ThreatFoxRecordMapper.IsCompromisedKey]!);
        Assert.False(withAbsent.Metadata!.ContainsKey(ThreatFoxRecordMapper.IsCompromisedKey));
    }

    [Fact]
    public void Zero_confidence_is_retained_and_distinct_from_absent()
    {
        var withZero = _mapper.Map(FullRecord() with { ConfidenceLevel = 0 }, 1);
        var withAbsent = _mapper.Map(FullRecord() with { ConfidenceLevel = null }, 1);

        Assert.Equal(0, withZero.Metadata![ThreatFoxRecordMapper.ConfidenceLevelKey]);
        Assert.False(withAbsent.Metadata!.ContainsKey(ThreatFoxRecordMapper.ConfidenceLevelKey));
    }

    [Fact]
    public void Empty_values_are_retained_while_absent_values_remain_null()
    {
        var withEmpty = _mapper.Map(FullRecord() with { Reference = "" }, 1);
        var withAbsent = _mapper.Map(FullRecord() with { Reference = null }, 1);

        Assert.Equal("", withEmpty.Metadata![ThreatFoxRecordMapper.ReferenceKey]);
        Assert.Null(withAbsent.LastSeenAt);
    }

    [Fact]
    public void Absent_ioc_fields_remain_null_or_absent_not_empty()
    {
        var record = new ThreatFoxJsonRecord
        {
            Id = "7",
            Ioc = "9.9.9.9",
            IocType = "ip:port",
            FirstSeen = "2026-01-01 00:00:00 UTC",
        };

        var input = _mapper.Map(record, 1);

        Assert.Null(input.DeclaredType);
        Assert.Null(input.LastSeenAt);
        Assert.Null(input.Tags);
        Assert.False(input.Metadata!.ContainsKey(ThreatFoxRecordMapper.MalwareKey));
        Assert.False(input.Metadata.ContainsKey(ThreatFoxRecordMapper.ConfidenceLevelKey));
    }

    [Fact]
    public void Ipv4_ioc_is_normalized_with_the_port_stored_separately()
    {
        var record = new ThreatFoxJsonRecord
        {
            Id = "8",
            Ioc = "185.132.53.205:1312",
            IocType = "ip:port",
            FirstSeen = "2026-01-04 00:00:00 UTC",
        };

        var input = _mapper.Map(record, 1);

        Assert.Equal("185.132.53.205:1312", input.RawIoc);
        Assert.Null(input.DeclaredType);
    }
}