namespace ThreatIntelligence.Domain.Entities;

public class IocTag
{
    public int IocId { get; set; }
    public int TagId { get; set; }

    public Ioc Ioc { get; set; } = null!;
    public Tag Tag { get; set; } = null!;
}
