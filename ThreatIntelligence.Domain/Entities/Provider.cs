namespace ThreatIntelligence.Domain;

public class Provider
{
    public Guid ProviderId { get; set; }
    public string Name { get; set; } = null!;
    public string BaseUrl { get; set; } = null!;
    public bool IsEnabled { get; set; }
    public int FetchIntervalMinutes { get; set; }
}
