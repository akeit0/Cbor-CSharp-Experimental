using Cbor.Testing;
using System.Formats.Cbor;

namespace Cbor.Tests.Conformance;

public sealed class UnsignedIntegerConformanceTests
{
    // RFC 8949 Appendix A; these test major type 0 only.
    public static TheoryData<ulong, string> RfcVectors => new()
    {
        { 0, "00" },
        { 1, "01" },
        { 10, "0a" },
        { 23, "17" },
        { 24, "1818" },
        { 25, "1819" },
        { 100, "1864" },
        { 1000, "1903e8" },
        { 1000000, "1a000f4240" },
        { 1000000000000, "1b000000e8d4a51000" },
        { ulong.MaxValue, "1bffffffffffffffff" },
    };

    [Theory]
    [MemberData(nameof(RfcVectors))]
    public void EncodingMatchesRfcAndBcl(ulong value, string hex)
    {
        byte[] expected = Convert.FromHexString(hex);
        byte[] actual = IntegerHarness.Encode(value);
        var oracle = new CborWriter(CborConformanceMode.Strict);
        oracle.WriteUInt64(value);

        Assert.Equal(expected, actual);
        Assert.Equal(expected, oracle.Encode());
        Assert.Equal(value, IntegerHarness.Decode(expected));
        var reader = new CborReader(actual, CborConformanceMode.Strict);
        Assert.Equal(value, reader.ReadUInt64());
        Assert.Equal(0, reader.BytesRemaining);
    }

    [Theory]
    [InlineData("1800")]
    [InlineData("190017")]
    [InlineData("1a00000018")]
    [InlineData("1b0000000000000018")]
    public void DecoderAcceptsWellFormedNonPreferredWidths(string hex)
    {
        byte[] encoded = Convert.FromHexString(hex);
        var oracle = new CborReader(encoded, CborConformanceMode.Lax);
        Assert.Equal(oracle.ReadUInt64(), IntegerHarness.Decode(encoded));
    }
}
