using ThreatIntelligence.Application.Normalization;
using ThreatIntelligence.Domain.Enums;

namespace ThreatIntelligence.UnitTests.Normalization;

public class Ipv6PortTests
{
    private readonly Ipv6Normalizer _normalizer = new();

    [Theory]
    [InlineData("[2001:db8::1]:443", "2001:db8::1", 443)]
    [InlineData("[2001:db8::1]:1", "2001:db8::1", 1)]
    [InlineData("[2001:db8::1]:65535", "2001:db8::1", 65535)]
    [InlineData("[::1]:8080", "::1", 8080)]
    [InlineData("[::]:80", "::", 80)]
    public void Bracketed_ipv6_with_port_parses_address_and_port_separately(
        string input, string expectedAddress, int expectedPort)
    {
        var output = _normalizer.Normalize(input);

        Assert.True(output.IsSuccess);
        var result = output.Result!;
        Assert.Equal(IocType.Ipv6, result.Type);
        Assert.Equal(expectedAddress, result.IocValue);
        Assert.Equal(expectedPort, result.Port);
    }

    [Theory]
    [InlineData("[2001:db8::1]")]
    [InlineData("[::1]")]
    public void Bracketed_ipv6_without_port_returns_null_port(string input)
    {
        var output = _normalizer.Normalize(input);

        Assert.True(output.IsSuccess);
        Assert.Null(output.Result!.Port);
    }

    [Fact]
    public void Uppercase_bracketed_address_with_port_is_lowercased()
    {
        var output = _normalizer.Normalize("[2001:DB8::1]:443");

        Assert.True(output.IsSuccess);
        Assert.Equal("2001:db8::1", output.Result!.IocValue);
        Assert.Equal(443, output.Result!.Port);
    }
}