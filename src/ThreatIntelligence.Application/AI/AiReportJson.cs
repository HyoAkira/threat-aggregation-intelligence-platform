using System.Text.Json;
using System.Text.Json.Serialization;

namespace ThreatIntelligence.Application.AI;

/// <summary>Shared JSON options for the AI prompt and event payloads.</summary>
public static class AiReportJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };
}
