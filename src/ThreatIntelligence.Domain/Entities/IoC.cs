using ThreatIntelligence.Domain.Enums;

namespace ThreatIntelligence.Domain.Entities;

public class IoC
{
    public int Id { get; set; }
    public int ProviderId { get; set; }
    public string ProviderRecordId { get; set; } = string.Empty;
    public string? ProviderDetailsLink { get; set; }
    public string IocValue { get; set; } = string.Empty;
    public byte[] IocValueHash { get; set; } = [];
    public IocType IocType { get; set; }
    public int? Port { get; set; }
    public string? ThreatType { get; set; }
    public string? Malware { get; set; }
    public string? MalwareAlias { get; set; }
    public string? MalwarePrintable { get; set; }
    public DateTime FirstSeenUtc { get; set; }
    public DateTime? LastSeenUtc { get; set; }
    public bool? IsCompromised { get; set; }
    public string? Reporter { get; set; }
    public bool? Anonymous { get; set; }
    public bool? IsVerified { get; set; }
    public string? Target { get; set; }
    public string? PhishTankDetails { get; set; }
    public bool? PhishTankOnline { get; set; }
    public string? UrlHost { get; set; }
    public string? ParentDomain { get; set; }
    public byte? ThreatFoxConfidenceLevel { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
