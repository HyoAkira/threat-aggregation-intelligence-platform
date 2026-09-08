namespace ThreatIntelligence.Domain.Entities;

public class Provider
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string BaseUrl { get; set; } = null!;
    public bool IsEnabled { get; set; }
    public int IntervalMinutes { get; set; }

    public ICollection<Ioc> Iocs { get; set; } = new List<Ioc>();
}
