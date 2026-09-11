using ThreatIntelligence.Application.Normalization;
using ThreatIntelligence.Domain.Enums;

namespace ThreatIntelligence.UnitTests.Normalization;

public class Ipv4PortTests
{
    private readonly Ipv4Normalizer _normalizer = new();

    [Theory]
    [InlineData("1.2.3.4:1", 1)]
    [InlineData("1.2.3.4:443", 443)]
    [InlineData("1.2.3.4:8080", 8080)]
    [InlineData("1.2.3.4:65535", 65535)]
    public void Valid_port_is_parsed_separately(string input, int expectedPort)
    {
        var output = _normalizer.Normalize(input);

        Assert.True(output.IsSuccess);
        var result = output.Result!;
        Assert.Equal(IocType.Ipv4, result.Type);
        Assert.Equal("1.2.3.4", result.IocValue);
        Assert.Equal(expectedPort, result.Port);
    }

    [Theory]
    [InlineData("1.2.3.4:0")]
    [InlineData("1.2.3.4:65536")]
    [InlineData("1.2.3.4:70000")]
    [InlineData("1.2.3.4:-1")]
    [InlineData("1.2.3.4:+443")]
    public void Out_of_range_port_is_rejected(string input)
    {
        var output = _normalizer.Normalize(input);

        Assert.True(output.IsError);
        Assert.Equal(NormalizationErrorCode.InvalidIpv4Port, output.Error!.Code);
    }

    [Theory]
    [InlineData("1.2.3.4:abc")]
    [InlineData("1.2.3.4: ")]
    [InlineData("1.2.3.4:44 3")]
    [InlineData("1.2.3.4:")]
    public void Non_decimal_port_is_rejected(string input)
    {
        var output = _normalizer.Normalize(input);

        Assert.True(output.IsError);
        Assert.Equal(NormalizationErrorCode.InvalidIpv4Port, output.Error!.Code);
    }

    [Fact]
    public void Bare_ipv4_has_null_port()
    {
        var output = _normalizer.Normalize("1.2.3.4");

        Assert.True(output.IsSuccess);
        Assert.Null(output.Result!.Port);
    }

    [Fact]
    public void Port_boundary_1_is_accepted()
    {
        var output = _normalizer.Normalize("10.0.0.1:1");

        Assert.True(output.IsSuccess);
        Assert.Equal(1, output.Result!.Port);
    }

    [Fact]
    public void Port_boundary_65535_is_accepted()
    {
        var output = _normalizer.Normalize("10.0.0.1:65535");

        Assert.True(output.IsSuccess);
        Assert.Equal(65535, output.Result!.Port);
    }

    [Fact]
    public void Port_boundary_0_is_rejected()
    {
        var output = _normalizer.Normalize("10.0.0.1:0");

        Assert.True(output.IsError);
        Assert.Equal(NormalizationErrorCode.InvalidIpv4Port, output.Error!.Code);
    }

    [Fact]
    public void Port_boundary_65536_is_rejected()
    {
        var output = _normalizer.Normalize("10.0.0.1:65536");

        Assert.True(output.IsError);
        Assert.Equal(NormalizationErrorCode.InvalidIpv4Port, output.Error!.Code);
    }
}
