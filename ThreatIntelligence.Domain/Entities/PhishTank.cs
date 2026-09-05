namespace ThreatIntelligence.Domain.Entities;

public class PhishTank
{
    public long Id { get; set; }
    public int PhishId { get; set; }
    public string Url { get; set; } = null!;
    public string PhishDetailUrl { get; set; } = null!;
    public DateTime SubmissionTime { get; set; }
    public bool Verified { get; set; }
    public DateTime? VerificationTime { get; set; }
    public bool Online { get; set; }
    public string? Target { get; set; }
    public string? Details { get; set; }
}
