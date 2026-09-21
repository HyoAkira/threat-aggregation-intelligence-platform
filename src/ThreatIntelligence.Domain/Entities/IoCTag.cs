using ThreatIntelligence.Domain.Common;

namespace ThreatIntelligence.Domain.Entities;

public class IoCTag
{
    private IoCTag()
    {
    }

    public IoCTag(int iocId, int tagId)
    {
        IocId = Guard.Positive(iocId, nameof(iocId));
        TagId = Guard.Positive(tagId, nameof(tagId));
    }

    public int Id { get; private set; }
    public int IocId { get; private set; }
    public int TagId { get; private set; }
}
