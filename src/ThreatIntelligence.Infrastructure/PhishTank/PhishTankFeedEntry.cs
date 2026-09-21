using System.Text.Json.Serialization;

namespace ThreatIntelligence.Infrastructure.PhishTank;

public sealed record PhishTankFeedEntry
{
    [JsonPropertyName("phish_id")]
    public int? PhishId { get; init; }

    [JsonPropertyName("url")]
    public string? Url { get; init; }

    [JsonPropertyName("phish_detail_url")]
    public string? PhishDetailUrl { get; init; }

    [JsonPropertyName("submission_time")]
    public string? SubmissionTime { get; init; }

    [JsonPropertyName("verification_time")]
    public string? VerificationTime { get; init; }

    [JsonPropertyName("verified")]
    public string? Verified { get; init; }

    [JsonPropertyName("online")]
    public string? Online { get; init; }

    [JsonPropertyName("target")]
    public string? Target { get; init; }

    [JsonPropertyName("details")]
    public IReadOnlyList<PhishTankDetailEntry>? Details { get; init; }
}

public sealed record PhishTankDetailEntry
{
    [JsonPropertyName("ip_address")]
    public string? IpAddress { get; init; }

    [JsonPropertyName("cidr_block")]
    public string? CidrBlock { get; init; }

    [JsonPropertyName("announcing_network")]
    public string? AnnouncingNetwork { get; init; }

    [JsonPropertyName("rir")]
    public string? Rir { get; init; }

    [JsonPropertyName("detail_time")]
    public string? DetailTime { get; init; }
}
