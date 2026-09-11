using ThreatIntelligence.Application.Normalization;
using ThreatIntelligence.Domain.Enums;

namespace ThreatIntelligence.UnitTests.Normalization;

public class Ipv4NormalizationTests
{
    private readonly Ipv4Normalizer _normalizer = new();

    [Theory]
    [InlineData("1.2.3.4")]
    [InlineData("0.0.0.0")]
    [InlineData("255.255.255.255")]
    [InlineData("10.0.0.1")]
    [InlineData("192.168.1.1")]
    public void Bare_canonical_ipv4_returns_dotted_decimal_with_null_port(string input)
    {
        var output = _normalizer.Normalize(input);

        Assert.True(output.IsSuccess);
        var result = output.Result!;
        Assert.Equal(IocType.Ipv4, result.Type);
        Assert.Equal(input, result.IocValue);
        Assert.Null(result.Port);
        Assert.Null(result.UrlHost);
        Assert.Null(result.ParentDomain);
    }

    [Theory]
    [InlineData("  1.2.3.4  ", "1.2.3.4")]
    [InlineData("1.2.3.4\n", "1.2.3.4")]
    [InlineData("\t1.2.3.4", "1.2.3.4")]
    public void Whitespace_is_trimmed(string input, string expected)
    {
        var output = _normalizer.Normalize(input);

        Assert.True(output.IsSuccess);
        Assert.Equal(expected, output.Result!.IocValue);
    }

    [Fact]
    public void Zero_address_normalizes_to_0_0_0_0()
    {
        var output = _normalizer.Normalize("0.0.0.0");

        Assert.True(output.IsSuccess);
        Assert.Equal("0.0.0.0", output.Result!.IocValue);
    }

    [Fact]
    public void Broadcast_address_normalizes_to_255_255_255_255()
    {
        var output = _normalizer.Normalize("255.255.255.255");

        Assert.True(output.IsSuccess);
        Assert.Equal("255.255.255.255", output.Result!.IocValue);
    }

    [Theory]
    [InlineData("01.2.3.4")]
    [InlineData("1.02.3.4")]
    [InlineData("1.2.03.4")]
    [InlineData("1.2.3.04")]
    [InlineData("001.2.3.4")]
    public void Leading_zero_octets_are_rejected(string input)
    {
        var output = _normalizer.Normalize(input);

        Assert.True(output.IsError);
        Assert.Equal(NormalizationErrorCode.InvalidInput, output.Error!.Code);
    }

    [Theory]
    [InlineData("1.2.3")]
    [InlineData("1.2")]
    [InlineData("1")]
    public void Missing_octets_are_rejected(string input)
    {
        var output = _normalizer.Normalize(input);

        Assert.True(output.IsError);
        Assert.Equal(NormalizationErrorCode.InvalidInput, output.Error!.Code);
    }

    [Theory]
    [InlineData("1.2.3.4.5")]
    [InlineData("1.2.3.4.5.6")]
    public void Extra_octets_are_rejected(string input)
    {
        var output = _normalizer.Normalize(input);

        Assert.True(output.IsError);
        Assert.Equal(NormalizationErrorCode.InvalidInput, output.Error!.Code);
    }

    [Theory]
    [InlineData("1.2.3.-1")]
    [InlineData("1.2.3.+1")]
    [InlineData("1.2.3.a")]
    [InlineData("1.2.3. ")]
    [InlineData("1.2.3.")]
    [InlineData(".1.2.3.4")]
    [InlineData("1..2.3.4")]
    public void Malformed_ipv4_is_rejected(string input)
    {
        var output = _normalizer.Normalize(input);

        Assert.True(output.IsError);
        Assert.Equal(NormalizationErrorCode.InvalidInput, output.Error!.Code);
    }

    [Theory]
    [InlineData("256.0.0.0")]
    [InlineData("0.0.0.256")]
    [InlineData("999.999.999.999")]
    public void Octets_above_255_are_rejected(string input)
    {
        var output = _normalizer.Normalize(input);

        Assert.True(output.IsError);
        Assert.Equal(NormalizationErrorCode.InvalidInput, output.Error!.Code);
    }
}
