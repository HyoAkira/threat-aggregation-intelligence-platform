using System.Globalization;
using System.Text.Json;
using ThreatIntelligence.API.Serialization;

namespace ThreatIntelligence.UnitTests.Serialization;

/// <summary>
/// Locks the UTC-at-the-boundary contract: SQL Server <c>datetime2</c> loses <see cref="DateTimeKind"/>,
/// so the API must still emit an explicit UTC designator and read values back as UTC. Every assertion
/// runs against the real registered options (<see cref="ApiJson.Create"/>) rather than a hand-rolled set.
/// </summary>
public sealed class UtcDateTimeJsonConverterTests
{
    private static readonly DateTimeOffset Reference =
        new(2026, 9, 15, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Unspecified_kind_serializes_with_z_and_keeps_the_same_clock_value()
    {
        var value = DateTime.SpecifyKind(Reference.DateTime, DateTimeKind.Unspecified);

        var json = JsonSerializer.Serialize(value, ApiJson.Create());

        Assert.Equal("\"2026-09-15T10:00:00Z\"", json);
    }

    [Fact]
    public void Utc_kind_serializes_with_z()
    {
        var value = new DateTime(2026, 9, 15, 10, 0, 0, DateTimeKind.Utc);

        var json = JsonSerializer.Serialize(value, ApiJson.Create());

        Assert.Equal("\"2026-09-15T10:00:00Z\"", json);
    }

    [Fact]
    public void Local_kind_serializes_as_the_equivalent_utc_instant()
    {
        var value = DateTime.SpecifyKind(Reference.DateTime, DateTimeKind.Local);
        var expectedUtc = value.ToUniversalTime();

        var json = JsonSerializer.Serialize(value, ApiJson.Create());
        var text = JsonSerializer.Deserialize<string>(json)!;
        var roundTripped = DateTimeOffset.Parse(text, CultureInfo.InvariantCulture);

        Assert.EndsWith("Z", text, StringComparison.Ordinal);
        Assert.Equal(expectedUtc, roundTripped.UtcDateTime);
    }

    [Fact]
    public void Nullable_null_is_omitted_by_the_shared_policy()
    {
        var json = JsonSerializer.Serialize(new NullableHolder { Optional = null }, ApiJson.Create());

        Assert.Equal("{}", json);
    }

    [Fact]
    public void Nullable_non_null_emits_the_utc_designator()
    {
        var json = JsonSerializer.Serialize(
            new NullableHolder { Optional = Reference.DateTime },
            ApiJson.Create());

        Assert.Equal("{\"optional\":\"2026-09-15T10:00:00Z\"}", json);
    }

    [Fact]
    public void Deserializing_a_z_string_yields_utc_kind()
    {
        var value = JsonSerializer.Deserialize<DateTime>("\"2026-09-15T10:00:00Z\"", ApiJson.Create());

        Assert.Equal(DateTimeKind.Utc, value.Kind);
        Assert.Equal(Reference.UtcDateTime, value);
    }

    [Fact]
    public void Deserializing_a_nullable_z_string_yields_utc_kind()
    {
        var value = JsonSerializer.Deserialize<DateTime?>("\"2026-09-15T10:00:00Z\"", ApiJson.Create());

        Assert.NotNull(value);
        Assert.Equal(DateTimeKind.Utc, value!.Value.Kind);
        Assert.Equal(Reference.UtcDateTime, value.Value);
    }

    [Fact]
    public void Deserializing_null_yields_null()
    {
        var value = JsonSerializer.Deserialize<DateTime?>("null", ApiJson.Create());

        Assert.Null(value);
    }

    private sealed class NullableHolder
    {
        public DateTime? Optional { get; init; }
    }
}
