using System.Text.Json;
using System.Text.Json.Serialization;

namespace ThreatIntelligence.API.Serialization;

/// <summary>
/// Single source of truth for the API's JSON contract. The null-omission policy is what makes the
/// source-attributed response omit provider-inapplicable properties while retaining stored
/// <c>false</c>, <c>0</c>, and empty values.
/// </summary>
public static class ApiJson
{
    public static void Configure(JsonSerializerOptions options)
    {
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    }

    public static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        Configure(options);
        return options;
    }
}
