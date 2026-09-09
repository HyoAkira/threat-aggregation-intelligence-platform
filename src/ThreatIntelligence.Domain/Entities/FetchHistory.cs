using ThreatIntelligence.Domain.Enums;

namespace ThreatIntelligence.Domain.Entities;

public class FetchHistory
{
    public int Id { get; set; }
    public Guid JobId { get; set; }
    public int ProviderId { get; set; }
    public FetchTrigger Trigger { get; set; }
    public FetchMode Mode { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public FetchStatus Status { get; set; }
    public int RecordsAdded { get; set; }
    public int RecordsUpdated { get; set; }
    public string? ErrorDetail { get; set; }
}
