using ThreatIntelligence.Domain.Enums;

namespace ThreatIntelligence.Domain.Entities;
public class Provider
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public int IntervalMinutes { get; set; }
    public FetchStatus? LastFetchStatus { get; set; }
    public DateTime? LastFetchAt { get; set; }
    public DateTime? NextFetchTime { get; set; }
}
