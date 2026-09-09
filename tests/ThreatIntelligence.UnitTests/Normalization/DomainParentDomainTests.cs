using ThreatIntelligence.Application.Normalization;
using ThreatIntelligence.Infrastructure.PublicSuffix;

namespace ThreatIntelligence.UnitTests.Normalization;

public class DomainParentDomainTests
{
    private readonly DomainNormalizer _normalizer = new(new PublicSuffixParentDomainResolver());

    [Theory]
    [InlineData("example.com", "example.com")]
    [InlineData("example.co.uk", "example.co.uk")]
    [InlineData("example.ac.uk", "example.ac.uk")]
    [InlineData("xn--mnchen-3ya.example", "xn--mnchen-3ya.example")]
    public void Apex_domain_resolves_to_itself_as_parent(string input, string expectedParent)
    {
        var result = _normalizer.Normalize(input).OrThrow();
        Assert.Equal(expectedParent, result.ParentDomain);
    }

    [Theory]
    [InlineData("api.example.com", "example.com")]
    [InlineData("www.example.net", "example.net")]
    [InlineData("api.example.co.uk", "example.co.uk")]
    [InlineData("deep.sub.api.example.co.uk", "example.co.uk")]
    [InlineData("www.example.ac.uk", "example.ac.uk")]
    public void Subdomain_resolves_to_registrable_parent(string input, string expectedParent)
    {
        var result = _normalizer.Normalize(input).OrThrow();
        Assert.Equal(expectedParent, result.ParentDomain);
    }

    [Fact]
    public void Apex_result_value_equals_parent_domain()
    {
        var result = _normalizer.Normalize("Example.CO.UK.").OrThrow();
        Assert.Equal("example.co.uk", result.IocValue);
        Assert.Equal("example.co.uk", result.ParentDomain);
    }
}