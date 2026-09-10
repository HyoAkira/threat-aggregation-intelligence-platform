using System.Text.Json.Serialization;

namespace ThreatIntelligence.Infrastructure.ThreatFox;

/// <summary>A single record in the ThreatFox JSON export (recent files or the full archive entry).</summary>
public sealed record ThreatFoxJsonRecord
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("ioc")]
    public string? Ioc { get; init; }

    [JsonPropertyName("ioc_type")]
    public string? IocType { get; init; }

    [JsonPropertyName("ioc_type_desc")]
    public string? IocTypeDesc { get; init; }

    [JsonPropertyName("threat_type")]
    public string? ThreatType { get; init; }

    [JsonPropertyName("threat_type_desc")]
    public string? ThreatTypeDesc { get; init; }

    [JsonPropertyName("malware")]
    public string? Malware { get; init; }

    [JsonPropertyName("malware_printable")]
    public string? MalwarePrintable { get; init; }

    [JsonPropertyName("malware_alias")]
    public string? MalwareAlias { get; init; }

    [JsonPropertyName("malware_malpedia")]
    public string? MalwareMalpedia { get; init; }

    [JsonPropertyName("confidence_level")]
    public int? ConfidenceLevel { get; init; }

    [JsonPropertyName("first_seen")]
    public string? FirstSeen { get; init; }

    [JsonPropertyName("last_seen")]
    public string? LastSeen { get; init; }

    [JsonPropertyName("reference")]
    public string? Reference { get; init; }

    [JsonPropertyName("reporter")]
    public string? Reporter { get; init; }

    [JsonPropertyName("is_compromised")]
    public bool? IsCompromised { get; init; }

    [JsonPropertyName("tags")]
    public IReadOnlyList<string>? Tags { get; init; }

    [JsonPropertyName("malware_samples")]
    public IReadOnlyList<ThreatFoxMalwareSample>? MalwareSamples { get; init; }
}

public sealed record ThreatFoxMalwareSample
{
    [JsonPropertyName("time_stamp")]
    public string? TimeStamp { get; init; }

    [JsonPropertyName("md5_hash")]
    public string? Md5Hash { get; init; }

    [JsonPropertyName("sha256_hash")]
    public string? Sha256Hash { get; init; }

    [JsonPropertyName("malware_bazaar")]
    public string? MalwareBazaar { get; init; }
}