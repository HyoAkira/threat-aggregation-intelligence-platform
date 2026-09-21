using ThreatIntelligence.Application.ProviderRecords;

namespace ThreatIntelligence.UnitTests.ProviderRecords;

public class ProviderTimestampValidationTests
{
    [Fact]
    public void Local_first_seen_fails_with_invalid_timestamp()
    {
        var input = ProviderValidationFactory.ValidInput() with
        {
            FirstSeenAt = new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Local)
        };

        var result = ProviderValidationFactory.CreateValidatorWithDefault().Validate(input, recordIndex: 1);

        Assert.False(result.IsValid);
        Assert.Equal(ProviderRecordFailureCode.InvalidTimestamp, result.Failure.Code);
        Assert.Equal("firstSeenAt", result.Failure.Field);
    }

    [Fact]
    public void Unspecified_first_seen_fails_with_invalid_timestamp()
    {
        var input = ProviderValidationFactory.ValidInput() with
        {
            FirstSeenAt = new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Unspecified)
        };

        var result = ProviderValidationFactory.CreateValidatorWithDefault().Validate(input, recordIndex: 1);

        Assert.Equal(ProviderRecordFailureCode.InvalidTimestamp, result.Failure.Code);
        Assert.Equal("firstSeenAt", result.Failure.Field);
    }

    [Fact]
    public void Local_last_seen_fails_with_invalid_timestamp_pointing_at_last_seen()
    {
        var input = ProviderValidationFactory.ValidInput() with
        {
            LastSeenAt = new DateTime(2026, 9, 7, 10, 30, 0, DateTimeKind.Local)
        };

        var result = ProviderValidationFactory.CreateValidatorWithDefault().Validate(input, recordIndex: 1);

        Assert.Equal(ProviderRecordFailureCode.InvalidTimestamp, result.Failure.Code);
        Assert.Equal("lastSeenAt", result.Failure.Field);
    }

    [Fact]
    public void Unspecified_last_seen_fails_with_invalid_timestamp()
    {
        var input = ProviderValidationFactory.ValidInput() with
        {
            LastSeenAt = new DateTime(2026, 9, 7, 10, 30, 0, DateTimeKind.Unspecified)
        };

        var result = ProviderValidationFactory.CreateValidatorWithDefault().Validate(input, recordIndex: 1);

        Assert.Equal(ProviderRecordFailureCode.InvalidTimestamp, result.Failure.Code);
        Assert.Equal("lastSeenAt", result.Failure.Field);
    }

    [Fact]
    public void Null_last_seen_passes_and_yields_null_last_seen_utc()
    {
        var input = ProviderValidationFactory.ValidInput() with { LastSeenAt = null };

        var result = ProviderValidationFactory.CreateValidatorWithDefault().Validate(input, recordIndex: 1);

        Assert.True(result.IsValid);
        Assert.Null(result.Normalized.LastSeenUtc);
    }

    [Fact]
    public void Utc_timestamps_pass_and_convert_to_utc_datetime_offsets()
    {
        var result = ProviderValidationFactory.CreateValidatorWithDefault()
            .Validate(ProviderValidationFactory.ValidInput(), recordIndex: 1);

        Assert.True(result.IsValid);
        Assert.Equal(TimeSpan.Zero, result.Normalized.FirstSeenUtc.Offset);
        Assert.Equal(DateTimeKind.Utc, result.Normalized.FirstSeenUtc.UtcDateTime.Kind);
        Assert.Equal(new DateTimeOffset(2026, 9, 1, 8, 0, 0, TimeSpan.Zero), result.Normalized.FirstSeenUtc);
        Assert.Equal(new DateTimeOffset(2026, 9, 7, 10, 30, 0, TimeSpan.Zero), result.Normalized.LastSeenUtc);
    }
}