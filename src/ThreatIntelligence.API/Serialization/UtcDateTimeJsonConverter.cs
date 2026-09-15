using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ThreatIntelligence.API.Serialization;

/// <summary>
/// Serializes every <see cref="DateTime"/> as an explicit UTC instant. SQL Server <c>datetime2</c>
/// columns do not preserve <see cref="DateTimeKind"/>, so EF Core materializes stored UTC values as
/// <see cref="DateTimeKind.Unspecified"/>. Without this converter System.Text.Json would omit the
/// trailing <c>Z</c> and clients would misread the value as local time. The value is always UTC by
/// contract (<c>Guard.Utc</c> enforces it on write).
/// </summary>
public sealed class UtcDateTimeJsonConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException($"Expected a UTC date-time string but found {reader.TokenType}.");
        }

        var value = reader.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new JsonException("Expected a UTC date-time string but found an empty value.");
        }

        var parsed = DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        return ToUtc(parsed);
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        => writer.WriteStringValue(ToUtc(value));

    /// <summary>
    /// Maps any <see cref="DateTimeKind"/> onto the same instant expressed as UTC. Unspecified values
    /// are treated as UTC without shifting the clock value; local values are converted correctly.
    /// </summary>
    internal static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
    };
}

/// <summary>
/// Nullable companion to <see cref="UtcDateTimeJsonConverter"/>. Nulls remain governed by the shared
/// <c>WhenWritingNull</c> policy configured in <see cref="ApiJson"/>.
/// </summary>
public sealed class NullableUtcDateTimeJsonConverter : JsonConverter<DateTime?>
{
    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        return new UtcDateTimeJsonConverter().Read(ref reader, typeof(DateTime), options);
    }

    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStringValue(UtcDateTimeJsonConverter.ToUtc(value.Value));
    }
}
