using System.Text.Json.Serialization;

namespace ThreatIntelligence.Domain.Enums;

public enum FetchMode : byte
{
    [JsonStringEnumMemberName("recent")]
    Recent = 0,

    [JsonStringEnumMemberName("full")]
    Full = 1
}
