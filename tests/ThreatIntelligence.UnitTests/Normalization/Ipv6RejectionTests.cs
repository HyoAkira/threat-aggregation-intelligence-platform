using ThreatIntelligence.Application.Normalization;

namespace ThreatIntelligence.UnitTests.Normalization;

public class Ipv6RejectionTests
{
    private readonly Ipv6Normalizer _normalizer = new();

    [Theory]
    [InlineData("[2001:db8::1")]
    [InlineData("2001:db8::1]")]
    [InlineData("2001:db8::1[")]
    public void Unmatched_brackets_are_rejected(string input)
    {
        var output = _normalizer.Normalize(input);

        Assert.True(output.IsError);
        Assert.Equal(NormalizationErrorCode.UnmatchedBrackets, output.Error!.Code);
    }

    [Theory]
    [InlineData("[2001:db8::1]443")]
    [InlineData("[2001:db8::1]abcd")]
    [InlineData("2001:db8::1]:443")]
    public void Content_after_closing_bracket_without_colon_is_rejected(string input)
    {
        var output = _normalizer.Normalize(input);

        Assert.True(output.IsError);
        Assert.Equal(NormalizationErrorCode.UnmatchedBrackets, output.Error!.Code);
    }

    [Theory]
    [InlineData("[2001:db8::1]:0")]
    [InlineData("[2001:db8::1]:65536")]
    [InlineData("[2001:db8::1]:70000")]
    [InlineData("[2001:db8::1]:-1")]
    [InlineData("[2001:db8::1]:+443")]
    [InlineData("[2001:db8::1]:")]
    public void Invalid_bracketed_ports_are_rejected(string input)
    {
        var output = _normalizer.Normalize(input);

        Assert.True(output.IsError);
        Assert.Equal(NormalizationErrorCode.InvalidIpv6Port, output.Error!.Code);
    }

    [Theory]
    [InlineData("[2001:db8::1]:abc")]
    [InlineData("[2001:db8::1]:44 3")]
    [InlineData("[2001:db8::1]:4.4")]
    public void Non_decimal_bracketed_ports_are_rejected(string input)
    {
        var output = _normalizer.Normalize(input);

        Assert.True(output.IsError);
        Assert.Equal(NormalizationErrorCode.InvalidIpv6Port, output.Error!.Code);
    }

    [Theory]
    [InlineData("2001:db8::zz")]
    [InlineData("2001:db8::gggg")]
    [InlineData("12345:67890:abcde:1234:5678:9abc:def0:1234")]
    [InlineData("1.2.3.4")]
    [InlineData("hello")]
    public void Invalid_ipv6_addresses_are_rejected(string input)
    {
        var output = _normalizer.Normalize(input);

        Assert.True(output.IsError);
        Assert.Equal(NormalizationErrorCode.InvalidIpv6, output.Error!.Code);
    }

    [Theory]
    [InlineData("fe80::1%eth0")]
    [InlineData("fe80::1%12")]
    public void Zone_ids_are_rejected(string input)
    {
        var output = _normalizer.Normalize(input);

        Assert.True(output.IsError);
        Assert.Equal(NormalizationErrorCode.InvalidIpv6, output.Error!.Code);
    }

    [Theory]
    [InlineData("2001:db8::1:65536")]
    [InlineData("2001:db8::1:99999")]
    public void Unbracketed_port_attempts_are_rejected(string input)
    {
        var output = _normalizer.Normalize(input);

        Assert.True(output.IsError);
        Assert.Equal(NormalizationErrorCode.InvalidIpv6, output.Error!.Code);
    }

    [Fact]
    public void Blank_input_is_rejected()
    {
        var output = _normalizer.Normalize("   ");

        Assert.True(output.IsError);
        Assert.Equal(NormalizationErrorCode.InvalidInput, output.Error!.Code);
    }
}