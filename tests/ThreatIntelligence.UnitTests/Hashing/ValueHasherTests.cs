using System.Text;
using ThreatIntelligence.Infrastructure.Hashing;

namespace ThreatIntelligence.UnitTests.Hashing;

public class ValueHasherTests
{
    private readonly Sha256ValueHasher _hasher = new();

    public static IEnumerable<object[]> FixedVectors()
    {
        yield return ["abc", "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad"];
        yield return ["example.com", "a379a6f6eeafb9a55e378c118034e2751e682fab9f2d30ab13d2125586ce1947"];
        yield return ["1.2.3.4", "6694f83c9f476da31f5df6bcc520034e7e57d421d247b9d34f49edbfc84a764c"];
        yield return ["", "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"];
    }

    [Theory]
    [MemberData(nameof(FixedVectors))]
    public void Fixed_vectors_match_sha256_over_utf8(string value, string expectedHex)
    {
        var expected = Convert.FromHexString(expectedHex);
        var actual = _hasher.Hash(value);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("api.example.co.uk")]
    [InlineData("https://data.phishtank.com/a")]
    public void Output_is_exactly_32_bytes(string value)
    {
        Assert.Equal(32, _hasher.Hash(value).Length);
    }

    [Fact]
    public void Hashing_is_deterministic()
    {
        var first = _hasher.Hash("example.com");
        var second = _hasher.Hash("example.com");
        Assert.Equal(first, second);
    }

    [Fact]
    public void Hash_is_over_utf8_bytes()
    {
        var value = "例子.com";
        var expected = Convert.FromHexString("70bc74d7088588b04f3245e4c1943af903ae0d172c80cc91554f2a66073e0ad9");
        Assert.Equal(expected, _hasher.Hash(value));
    }
}

public class ValueEqualityTests
{
    private readonly Sha256ValueHasher _hasher = new();

    [Fact]
    public void Equal_value_round_trip_matches()
    {
        var value = "example.com";
        var hash = _hasher.Hash(value);
        Assert.True(ValueEquality.Matches(hash, value, value, _hasher));
    }

    [Fact]
    public void Different_value_with_different_hash_does_not_match()
    {
        var hash = _hasher.Hash("example.com");
        Assert.False(ValueEquality.Matches(hash, "example.com", "other.com", _hasher));
    }

    [Fact]
    public void Simulated_hash_collision_is_detected_by_value_comparison()
    {
        var storedValue = "example.com";
        var incomingValue = "different.com";
        var collisionHash = _hasher.Hash(incomingValue);

        Assert.False(ValueEquality.Matches(collisionHash, storedValue, incomingValue, _hasher));
    }

    [Fact]
    public void Stored_value_can_be_recovered_from_lookup()
    {
        var stored = new IoCValue("example.com", _hasher.Hash("example.com"));
        var candidate = _hasher.Hash(stored.Value);

        Assert.True(ValueEquality.Matches(candidate, stored.Value, stored.Value, _hasher));
    }

    private sealed record IoCValue(string Value, byte[] Hash);
}