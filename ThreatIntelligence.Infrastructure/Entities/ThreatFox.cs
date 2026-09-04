namespace ThreatIntelligence.Infrastructure;

public class ThreatFox
{
    public long Id { get; set; }
    public int IocId { get; set; }
    public string IocValue { get; set; } = null!;
    public string IocType { get; set; } = null!;
    public string ThreatType { get; set; } = null!;
    public string Malware { get; set; } = null!;
    public string? MalwareAlias { get; set; }
    public string MalwarePrintable { get; set; } = null!;
    public DateTime FirstSeenUtc { get; set; }
    public DateTime? LastSeenUtc { get; set; }
    public int ConfidenceLevel { get; set; }
    public bool IsCompromised { get; set; }
    public string? Reference { get; set; }
    public string? Tags { get; set; }
    public int Anonymous { get; set; }
    public string Reporter { get; set; } = null!;
}
