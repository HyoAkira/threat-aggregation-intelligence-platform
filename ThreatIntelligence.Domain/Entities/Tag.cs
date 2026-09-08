namespace ThreatIntelligence.Domain.Entities;

public class Tag
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;

    public ICollection<IocTag> IocTags { get; set; } = new List<IocTag>();
}
