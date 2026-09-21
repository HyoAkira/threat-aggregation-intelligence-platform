using ThreatIntelligence.Application.Fetches;
using ThreatIntelligence.Domain.Enums;

namespace ThreatIntelligence.UnitTests.Fetches;

public sealed class FetchTokensTests
{
    [Theory]
    [InlineData("recent", FetchMode.Recent)]
    [InlineData("full", FetchMode.Full)]
    [InlineData("RECENT", FetchMode.Recent)]
    [InlineData("Full", FetchMode.Full)]
    public void ParseMode_round_trips_the_documented_tokens(string token, FetchMode expected)
    {
        Assert.Equal(expected, FetchTokens.ParseMode(token));
        Assert.Equal(token.ToLowerInvariant(), FetchTokens.Mode(expected));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("hourly")]
    public void ParseMode_rejects_unknown_tokens(string? token)
    {
        Assert.Null(FetchTokens.ParseMode(token));
    }
}
