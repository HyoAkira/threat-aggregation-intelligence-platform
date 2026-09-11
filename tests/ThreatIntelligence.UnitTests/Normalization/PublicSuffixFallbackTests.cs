using ThreatIntelligence.Infrastructure.PublicSuffix;

namespace ThreatIntelligence.UnitTests.Normalization;

public class PublicSuffixFallbackTests
{
    private readonly PublicSuffixParentDomainResolver _resolver = new();

    [Theory]
    [InlineData("1.2.3.4")]
    [InlineData("10.0.0.1")]
    [InlineData("2001:db8::1")]
    [InlineData("::1")]
    public void Ip_literals_are_returned_as_is(string host)
    {
        Assert.Equal(host, _resolver.Resolve(host));
    }

    [Theory]
    [InlineData("localhost")]
    [InlineData("myserver")]
    [InlineData("myhost")]
    public void Single_label_hosts_are_returned_as_is(string host)
    {
        Assert.Equal(host, _resolver.Resolve(host));
    }

    [Fact]
    public void Unknown_suffix_follows_prevailing_rule()
    {
        Assert.Equal("example.unknown", _resolver.Resolve("a.example.unknown"));
    }

    [Fact]
    public void Unknown_suffix_multi_level_is_reduced_to_registrable()
    {
        Assert.Equal("example.zz", _resolver.Resolve("a.b.example.zz"));
    }

    [Fact]
    public void Public_suffix_only_has_no_registrable_domain()
    {
        Assert.Null(_resolver.Resolve("co.uk"));
    }

    [Fact]
    public void Blank_input_returns_null()
    {
        Assert.Null(_resolver.Resolve("   "));
        Assert.Null(_resolver.Resolve(""));
    }
}