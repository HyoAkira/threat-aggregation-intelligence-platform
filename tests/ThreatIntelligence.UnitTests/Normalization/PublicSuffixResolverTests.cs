using ThreatIntelligence.Infrastructure.PublicSuffix;

namespace ThreatIntelligence.UnitTests.Normalization;

public class PublicSuffixResolverTests
{
    private readonly PublicSuffixParentDomainResolver _resolver = new();

    [Theory]
    [InlineData("example.com", "example.com")]
    [InlineData("example.co.uk", "example.co.uk")]
    [InlineData("example.ac.uk", "example.ac.uk")]
    [InlineData("a.com", "a.com")]
    public void Apex_domain_resolves_to_itself(string input, string expected)
    {
        Assert.Equal(expected, _resolver.Resolve(input));
    }

    [Theory]
    [InlineData("api.example.com", "example.com")]
    [InlineData("api.example.co.uk", "example.co.uk")]
    [InlineData("deep.sub.api.example.co.uk", "example.co.uk")]
    [InlineData("www.example.net", "example.net")]
    public void Subdomain_resolves_to_registrable_domain(string input, string expected)
    {
        Assert.Equal(expected, _resolver.Resolve(input));
    }

    [Fact]
    public void Multi_level_suffix_co_uk_is_honored()
    {
        Assert.Equal("example.co.uk", _resolver.Resolve("api.example.co.uk"));
    }

    [Fact]
    public void Exception_rule_www_ck_is_honored()
    {
        Assert.Equal("www.ck", _resolver.Resolve("www.ck"));
        Assert.Equal("www.ck", _resolver.Resolve("a.www.ck"));
    }

    [Fact]
    public void Wildcard_rule_ck_reduces_to_the_registrable_below_the_wildcard()
    {
        Assert.Equal("y.foo.ck", _resolver.Resolve("x.y.foo.ck"));
    }

    [Fact]
    public void Wildcard_rule_ck_treats_the_wildcard_suffix_as_registrable_apex()
    {
        Assert.Equal("a.foo.ck", _resolver.Resolve("a.foo.ck"));
    }

    [Fact]
    public void Public_suffix_alone_has_no_registrable_domain()
    {
        Assert.Null(_resolver.Resolve("co.uk"));
    }
}