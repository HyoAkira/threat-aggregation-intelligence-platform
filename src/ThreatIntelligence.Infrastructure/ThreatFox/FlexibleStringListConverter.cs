using System.Text.Json;
using System.Text.Json.Serialization;

namespace ThreatIntelligence.Infrastructure.ThreatFox;

/// <summary>
/// Reads a ThreatFox tag field that may be serialized either as a comma-separated JSON string
/// (the export files) or as a JSON array of strings (the community API shape). Null, blank, or
/// empty values yield <c>null</c> so downstream normalization keeps treating "no tags" as absent.
/// </summary>
internal sealed class FlexibleStringListConverter : JsonConverter<IReadOnlyList<string>?>
{
    public override IReadOnlyList<string>? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return null;

            case JsonTokenType.String:
                var raw = reader.GetString();
                if (string.IsNullOrWhiteSpace(raw))
                {
                    return null;
                }

                var split = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                return split.Length > 0 ? split : null;

            case JsonTokenType.StartArray:
                var values = new List<string>();
                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                {
                    if (reader.TokenType != JsonTokenType.String)
                    {
                        throw new JsonException($"Unexpected token {reader.TokenType} inside a tags array.");
                    }

                    var value = reader.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        values.Add(value);
                    }
                }

                return values.Count > 0 ? values : null;

            default:
                throw new JsonException($"Unexpected token {reader.TokenType} for tags.");
        }
    }

    public override void Write(
        Utf8JsonWriter writer,
        IReadOnlyList<string>? value,
        JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartArray();
        foreach (var item in value)
        {
            writer.WriteStringValue(item);
        }

        writer.WriteEndArray();
    }
}
