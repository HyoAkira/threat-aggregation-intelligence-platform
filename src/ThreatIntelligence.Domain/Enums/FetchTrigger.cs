using System.Text.Json.Serialization;

namespace ThreatIntelligence.Domain.Enums;

public enum FetchTrigger : byte
{
    [JsonStringEnumMemberName("scheduled")]
    Scheduled = 0,

    [JsonStringEnumMemberName("manual")]
    Manual = 1
}
