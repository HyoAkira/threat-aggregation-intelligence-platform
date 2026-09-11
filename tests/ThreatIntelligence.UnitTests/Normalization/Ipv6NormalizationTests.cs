using ThreatIntelligence.Application.Normalization;
using ThreatIntelligence.Domain.Enums;

namespace ThreatIntelligence.UnitTests.Normalization;

public class Ipv6NormalizationTests
{
    private readonly Ipv6Normalizer _normalizer = new();

    [Theory]
    [InlineData("2001:db8::1", "2001:db8::1")]
    [InlineData("::1", "::1")]
    [InlineData("::", "::")]
    [InlineData("2001:0db8:0000:0000:0000:0000:0000:0001", "2001:db8::1")]
    [InlineData("2001:db8:0:0:0:0:0:1", "2001:db8::1")]
    [InlineData("fe80:0:0:0:0:0:0:1", "fe80::1")]
    public void Bare_ipv6_normalizes_to_canonical_form(string input, string expected)
    {
        var output = _normalizer.Normalize(input);

        Assert.True(output.IsSuccess);
        var result = output.Result!;
        Assert.Equal(IocType.Ipv6, result.Type);
        Assert.Equal(expected, result.IocValue);
        Assert.Null(result.Port);
        Assert.Null(result.UrlHost);
        Assert.Null(result.ParentDomain);
    }

    [Theory]
    [InlineData("2001:DB8::1", "2001:db8::1")]
    [InlineData("2001:0DB8:0000:0000:0000:0000:0000:0001", "2001:db8::1")]
    [InlineData("ABCD::EF01", "abcd::ef01")]
    public void Uppercase_ipv6_is_lowercased(string input, string expected)
    {
        var output = _normalizer.Normalize(input);

        Assert.True(output.IsSuccess);
        Assert.Equal(expected, output.Result!.IocValue);
    }

    [Fact]
    public void Expanded_and_compressed_equivalents_produce_identical_values()
    {
        var expanded = _normalizer.Normalize("2001:0db8:0000:0000:0000:0000:0000:0001");
        var compressed = _normalizer.Normalize("2001:db8::1");

        Assert.True(expanded.IsSuccess);
        Assert.True(compressed.IsSuccess);
        Assert.Equal(expanded.Result!.IocValue, compressed.Result!.IocValue);
    }

    [Theory]
    [InlineData("::ffff:192.168.1.1")]
    [InlineData("::ffff:0:1.2.3.4")]
    public void Ipv4_mapped_ipv6_is_supported(string input)
    {
        var output = _normalizer.Normalize(input);

        Assert.True(output.IsSuccess);
        Assert.Equal(IocType.Ipv6, output.Result!.Type);
        Assert.Null(output.Result!.Port);
    }

    [Theory]
    [InlineData("  2001:db8::1  ", "2001:db8::1")]
    [InlineData("\n2001:db8::1\t", "2001:db8::1")]
    public void Whitespace_is_trimmed(string input, string expected)
    {
        var output = _normalizer.Normalize(input);

        Assert.True(output.IsSuccess);
        Assert.Equal(expected, output.Result!.IocValue);
    }

    [Fact]
    public void Bare_ipv6_keeps_colons_as_address_syntax_with_null_port()
    {
        var output = _normalizer.Normalize("2001:db8::1");

        Assert.True(output.IsSuccess);
        Assert.Equal("2001:db8::1", output.Result!.IocValue);
        Assert.Null(output.Result!.Port);
    }
}