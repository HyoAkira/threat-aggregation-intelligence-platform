using System.Globalization;
using System.Text.Json;
using Serilog.Events;
using Serilog.Parsing;

namespace ThreatIntelligence.Infrastructure.Admin;

/// <summary>
/// Renders a CLEF message template (<c>@mt</c>) using the properties captured on the same line, so
/// the Admin log tail shows the filled-in message instead of the raw template. Holes with a format
/// prefer the pre-rendered <c>@r</c> values (paired positionally with the template's format-specified
/// holes), string holes without a format render literally (unquoted, like the console sink's default
/// <c>{Message:lj}</c>), and unknown properties keep the literal hole text so a partially written
/// line degrades instead of failing.
/// </summary>
public static class ClefMessageRenderer
{
    public static string Render(JsonElement root, string templateText)
    {
        var template = new MessageTemplateParser().Parse(templateText);
        var renderings = ReadRenderings(root);
        var nextRendering = 0;
        var writer = new StringWriter(CultureInfo.InvariantCulture);
        foreach (var token in template.Tokens)
        {
            if (token is TextToken text)
            {
                writer.Write(text.Text);
            }
            else if (token is PropertyToken property)
            {
                string? preRendered = null;
                if (property.Format is not null && renderings is not null)
                {
                    if (nextRendering < renderings.Count)
                    {
                        preRendered = renderings[nextRendering];
                    }

                    nextRendering++;
                }

                if (preRendered is not null)
                {
                    writer.Write(preRendered);
                }
                else if (TryConvert(root, property.PropertyName, out var value))
                {
                    var format = property.Format;
                    if (format is null && value is ScalarValue { Value: string })
                    {
                        format = "l";
                    }

                    value.Render(writer, format, CultureInfo.InvariantCulture);
                }
                else
                {
                    writer.Write(token.ToString());
                }
            }
        }

        return writer.ToString();
    }

    private static List<string>? ReadRenderings(JsonElement root)
    {
        if (!root.TryGetProperty("@r", out var preRendered) || preRendered.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var values = preRendered.EnumerateArray()
            .Where(element => element.ValueKind == JsonValueKind.String)
            .Select(element => element.GetString()!)
            .ToList();
        return values.Count > 0 ? values : null;
    }

    private static bool TryConvert(JsonElement root, string name, out LogEventPropertyValue value)
    {
        value = null!;
        if (name.StartsWith('@') || !root.TryGetProperty(name, out var element))
        {
            return false;
        }

        value = Convert(element);
        return true;
    }

    private static LogEventPropertyValue Convert(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => new ScalarValue(element.GetString()),
        JsonValueKind.Number when decimal.TryParse(
            element.GetRawText(),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var number) => new ScalarValue(number),
        JsonValueKind.True => new ScalarValue(true),
        JsonValueKind.False => new ScalarValue(false),
        JsonValueKind.Null => new ScalarValue(null),
        JsonValueKind.Array => new SequenceValue(element.EnumerateArray().Select(Convert)),
        JsonValueKind.Object => new DictionaryValue(element.EnumerateObject().Select(property =>
            new KeyValuePair<ScalarValue, LogEventPropertyValue>(
                new ScalarValue(property.Name),
                Convert(property.Value)))),
        _ => new ScalarValue(null)
    };
}
