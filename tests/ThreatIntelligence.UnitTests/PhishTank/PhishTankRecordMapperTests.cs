using System.Text.Json;
using ThreatIntelligence.Application.ProviderRecords;
using ThreatIntelligence.Domain.Enums;
using ThreatIntelligence.Infrastructure.PhishTank;

namespace ThreatIntelligence.UnitTests.PhishTank;

public sealed class PhishTankRecordMapperTests
{
    private readonly PhishTankRecordMapper _mapper = new();

    private static PhishTankFeedEntry FullRecord() => new()
    {
        PhishId = 12345,
        Url = "http://evil.example.com/phish",
        PhishDetailUrl = "http://data.phishtank.com/data/12345/detail",
        SubmissionTime = "2026-01-01T12:00:00+00:00",
        VerificationTime = "2026-01-01T12:30:00+00:00",
        Verified = "yes",
        Online = "yes",
        Target = "Example Bank",
        Details =
        [
            new PhishTankDetailEntry
            {
                IpAddress = "1.2.3.4",
                CidrBlock = "1.2.3.0/24",
                AnnouncingNetwork = "1234",
                Rir = "arin",
                DetailTime = "2026-01-01T12:30:00+00:00"
            },
            new PhishTankDetailEntry
            {
                IpAddress = "5.6.7.8",
                CidrBlock = "5.6.7.0/24",
                AnnouncingNetwork = "5678",
                Rir = "ripe",
                DetailTime = "2026-01-01T12:31:00+00:00"
            }
        ]
    };

    [Fact]
    public void Every_documented_field_is_mapped_to_its_canonical_location()
    {
        var input = _mapper.Map(FullRecord(), recordIndex: 1);

        Assert.Equal(PhishTankProviderValidator.PhishTankProviderId, input.ProviderId);
        Assert.Equal("12345", input.ProviderRecordId);
        Assert.Equal("http://evil.example.com/phish", input.RawIoc);
        Assert.Equal(IocType.Url, input.DeclaredType);
        Assert.Equal(new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc), input.FirstSeenAt);
        Assert.Equal(new DateTime(2026, 1, 1, 12, 30, 0, DateTimeKind.Utc), input.LastSeenAt);
        Assert.Equal("http://data.phishtank.com/data/12345/detail", input.ProviderDetailsLink);
        Assert.Null(input.Tags);

        var metadata = input.Metadata!;
        Assert.Equal(true, metadata[PhishTankRecordMapper.VerifiedKey]);
        Assert.Equal(true, metadata[PhishTankRecordMapper.OnlineKey]);
        Assert.Equal("Example Bank", metadata[PhishTankRecordMapper.TargetKey]);
    }

    [Fact]
    public void Details_are_serialized_as_json_in_payload()
    {
        var input = _mapper.Map(FullRecord(), recordIndex: 1);

        Assert.NotNull(input.Payload);
        Assert.True(input.Payload!.ContainsKey(PhishTankRecordMapper.PhishTankDetailsKey));

        var detailsJson = input.Payload[PhishTankRecordMapper.PhishTankDetailsKey] as string;
        Assert.NotNull(detailsJson);

        var details = JsonSerializer.Deserialize<List<PhishTankDetailEntry>>(detailsJson!);
        Assert.NotNull(details);
        Assert.Equal(2, details!.Count);
        Assert.Equal("1.2.3.4", details[0].IpAddress);
        Assert.Equal("1.2.3.0/24", details[0].CidrBlock);
        Assert.Equal("1234", details[0].AnnouncingNetwork);
        Assert.Equal("arin", details[0].Rir);
        Assert.Equal("2026-01-01T12:30:00+00:00", details[0].DetailTime);
        Assert.Equal("5.6.7.8", details[1].IpAddress);
    }

    [Fact]
    public void No_details_yields_null_payload()
    {
        var record = FullRecord() with { Details = null };
        var input = _mapper.Map(record, 1);

        Assert.Null(input.Payload);
    }

    [Fact]
    public void Empty_details_yields_null_payload()
    {
        var record = FullRecord() with { Details = [] };
        var input = _mapper.Map(record, 1);

        Assert.Null(input.Payload);
    }

    [Fact]
    public void False_boolean_is_retained_and_distinct_from_absent()
    {
        var withFalse = _mapper.Map(FullRecord() with { Verified = "no", Online = "no" }, 1);
        var withAbsent = _mapper.Map(FullRecord() with { Verified = null, Online = null }, 1);

        Assert.Equal(false, withFalse.Metadata![PhishTankRecordMapper.VerifiedKey]);
        Assert.Equal(false, withFalse.Metadata[PhishTankRecordMapper.OnlineKey]);
        Assert.False(withAbsent.Metadata!.ContainsKey(PhishTankRecordMapper.VerifiedKey));
        Assert.False(withAbsent.Metadata.ContainsKey(PhishTankRecordMapper.OnlineKey));
    }

    [Fact]
    public void Empty_string_booleans_are_treated_as_absent()
    {
        var input = _mapper.Map(FullRecord() with { Verified = "", Online = "" }, 1);

        Assert.False(input.Metadata!.ContainsKey(PhishTankRecordMapper.VerifiedKey));
        Assert.False(input.Metadata.ContainsKey(PhishTankRecordMapper.OnlineKey));
    }

    [Fact]
    public void No_verification_time_yields_null_last_seen()
    {
        var record = FullRecord() with { VerificationTime = null };
        var input = _mapper.Map(record, 1);

        Assert.Null(input.LastSeenAt);
    }

    [Fact]
    public void Phish_id_must_be_positive()
    {
        var record = FullRecord() with { PhishId = 0 };

        var ex = Assert.Throws<PhishTankRecordParseException>(() => _mapper.Map(record, 1));
        Assert.Equal(1, ex.RecordIndex);
        Assert.Equal("phish_id", ex.Field);
    }

    [Fact]
    public void Null_phish_id_is_rejected()
    {
        var record = FullRecord() with { PhishId = null };

        var ex = Assert.Throws<PhishTankRecordParseException>(() => _mapper.Map(record, 1));
        Assert.Equal(1, ex.RecordIndex);
        Assert.Equal("phish_id", ex.Field);
    }

    [Fact]
    public void Blank_url_is_rejected()
    {
        var record = FullRecord() with { Url = "  " };

        var ex = Assert.Throws<PhishTankRecordParseException>(() => _mapper.Map(record, 1));
        Assert.Equal(1, ex.RecordIndex);
        Assert.Equal("url", ex.Field);
    }

    [Fact]
    public void Blank_submission_time_is_rejected()
    {
        var record = FullRecord() with { SubmissionTime = "" };

        var ex = Assert.Throws<PhishTankRecordParseException>(() => _mapper.Map(record, 1));
        Assert.Equal(1, ex.RecordIndex);
        Assert.Equal("submission_time", ex.Field);
    }

    [Fact]
    public void Invalid_submission_time_format_is_rejected()
    {
        var record = FullRecord() with { SubmissionTime = "not-a-date" };

        var ex = Assert.Throws<PhishTankRecordParseException>(() => _mapper.Map(record, 1));
        Assert.Equal(1, ex.RecordIndex);
        Assert.Equal("submission_time", ex.Field);
    }

    [Fact]
    public void Null_record_is_rejected()
    {
        var ex = Assert.Throws<PhishTankRecordParseException>(() => _mapper.Map(null!, 1));
        Assert.Equal(1, ex.RecordIndex);
        Assert.Equal("record", ex.Field);
    }

    [Fact]
    public void Detail_IPs_are_preserved_in_payload_not_as_separate_iocs()
    {
        var input = _mapper.Map(FullRecord(), 1);

        Assert.Null(input.RawIoc.Contains("1.2.3.4") ? "rawIoc should not contain detail IPs" : null);
        var detailsJson = input.Payload![PhishTankRecordMapper.PhishTankDetailsKey] as string;
        Assert.Contains("1.2.3.4", detailsJson!);
    }
}
