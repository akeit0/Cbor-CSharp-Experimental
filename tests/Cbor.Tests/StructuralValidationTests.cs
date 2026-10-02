using System.Buffers;
using Cbor.Testing;

namespace Cbor.Tests;

public sealed class StructuralValidationTests
{
    public static TheoryData<string> Valid => new(RfcVectors.Valid);
    public static TheoryData<string> Invalid => new(RfcVectors.Invalid);

    [Fact]
    public void RfcFixtureCountIsStable() => Assert.Equal(81, RfcVectors.Valid.Length);

    [Theory]
    [MemberData(nameof(Valid))]
    public void EveryRfcVectorAndEverySeamValidate(string hex)
    {
        byte[] encoded = Convert.FromHexString(hex);
        Assert.Equal(CborDecodeResult.Success, CborValidation.TryValidate(encoded));
        ReadOnlySequence<byte> fragmented = IntegerHarness.Fragment(encoded);
        Assert.Equal(CborDecodeResult.Success, CborValidation.TryValidate(in fragmented));
        for (int offset = 0; offset <= encoded.Length; offset++)
        {
            ReadOnlySequence<byte> source = IntegerHarness.Split(encoded, offset);
            Assert.Equal(CborDecodeResult.Success, CborValidation.TryValidate(in source));
        }
    }

    [Theory]
    [MemberData(nameof(Valid))]
    public void EveryTruncatedPrefixIsIncomplete(string hex)
    {
        byte[] encoded = Convert.FromHexString(hex);
        for (int length = 0; length < encoded.Length; length++)
        {
            byte[] prefix = encoded.AsSpan(0, length).ToArray();
            Assert.Equal(CborDecodeResult.NeedMoreData, CborValidation.TryReadValueLength(prefix, out int consumed));
            Assert.Equal(0, consumed);
            ReadOnlySequence<byte> source = IntegerHarness.Split(prefix, length / 2);
            Assert.Equal(CborDecodeResult.NeedMoreData, CborValidation.TryReadValueLength(in source, out long sequenceConsumed));
            Assert.Equal(0, sequenceConsumed);
        }
    }

    [Theory]
    [MemberData(nameof(Invalid))]
    public void RfcMalformedVectorsNeverValidate(string hex)
    {
        byte[] encoded = Convert.FromHexString(hex);
        Assert.NotEqual(CborDecodeResult.Success, CborValidation.TryValidate(encoded));
        for (int offset = 0; offset <= encoded.Length; offset++)
        {
            ReadOnlySequence<byte> source = IntegerHarness.Split(encoded, offset);
            Assert.NotEqual(CborDecodeResult.Success, CborValidation.TryValidate(in source));
        }
    }

    [Fact]
    public void PrefixScanningPreservesFollowingItems()
    {
        byte[] sequence = Convert.FromHexString("83010203f6");
        Assert.Equal(CborDecodeResult.Success, CborValidation.TryReadValueLength(sequence, out int consumed));
        Assert.Equal(4, consumed);
        Assert.Equal(CborDecodeResult.InvalidData, CborValidation.TryValidate(sequence));
    }

    [Theory]
    [InlineData("61ff")]
    [InlineData("62c080")]
    [InlineData("63eda080")]
    [InlineData("64f4908080")]
    [InlineData("62e282")]
    [InlineData("7f61c361bcff")] // A code point may cross a segment, but may not cross CBOR text chunks.
    public void InvalidUtf8IsRejectedAcrossEverySeam(string hex)
    {
        byte[] encoded = Convert.FromHexString(hex);
        Assert.Equal(CborDecodeResult.InvalidData, CborValidation.TryValidate(encoded));
        for (int offset = 0; offset <= encoded.Length; offset++)
        {
            var source = IntegerHarness.Split(encoded, offset);
            Assert.Equal(CborDecodeResult.InvalidData, CborValidation.TryValidate(in source));
        }
    }
}
