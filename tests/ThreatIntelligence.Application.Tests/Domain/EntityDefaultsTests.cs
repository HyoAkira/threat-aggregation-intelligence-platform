using ThreatIntelligence.Domain.Entities;

namespace ThreatIntelligence.Application.Tests.Domain;

public class EntityDefaultsTests
{
    [Fact]
    public void CommonModelNavigationsAreInitialized()
    {
        var provider = new Provider();
        var ioc = new Ioc();
        var tag = new Tag();

        Assert.Empty(provider.Iocs);
        Assert.Empty(ioc.IocTags);
        Assert.Empty(tag.IocTags);
    }

    [Fact]
    public void IocCanReferenceItsProvider()
    {
        var provider = new Provider { Id = 1 };
        var ioc = new Ioc { ProviderId = provider.Id, Provider = provider };

        provider.Iocs.Add(ioc);

        Assert.Same(provider, ioc.Provider);
        Assert.Contains(ioc, provider.Iocs);
    }
}
