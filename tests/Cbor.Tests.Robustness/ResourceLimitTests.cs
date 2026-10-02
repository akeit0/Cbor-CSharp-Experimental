using Cbor.Testing;

namespace Cbor.Tests.Robustness;

public sealed class ResourceLimitTests
{
    [Fact]
    public void DepthLimitIncludesTagsAndEmptyContainersWithoutRecursion()
    {
        byte[] encoded = Enumerable.Repeat((byte)0x81, 1024).Append((byte)0x00).ToArray();
        Assert.Equal(CborDecodeResult.Success, CborValidation.TryValidate(encoded, new CborReaderOptions(maxDepth: 1024)));
        Assert.Equal(CborDecodeResult.LimitExceeded, CborValidation.TryValidate(encoded, new CborReaderOptions(maxDepth: 1023)));

        Assert.Equal(CborDecodeResult.Success, CborValidation.TryValidate([0x00], new CborReaderOptions(maxDepth: 0)));
        Assert.Equal(CborDecodeResult.LimitExceeded, CborValidation.TryValidate([0x80], new CborReaderOptions(maxDepth: 0)));
        Assert.Equal(CborDecodeResult.LimitExceeded, CborValidation.TryValidate([0xc0, 0x00], new CborReaderOptions(maxDepth: 0)));
    }

    [Theory]
    [InlineData("5bffffffffffffffff")]
    [InlineData("7bffffffffffffffff")]
    [InlineData("9bffffffffffffffff")]
    [InlineData("bbffffffffffffffff")]
    public void HugeDeclaredLengthsAreRejectedBeforeWorkOrAllocation(string hex)
    {
        byte[] encoded = Convert.FromHexString(hex);
        var options = CborReaderOptions.Default;
        long before = GC.GetAllocatedBytesForCurrentThread();
        var result = CborValidation.TryValidate(encoded, options);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(CborDecodeResult.LimitExceeded, result);
        Assert.Equal(0, allocated);
    }

    [Fact]
    public void ByteAndItemLimitsHaveExactInclusiveBoundaries()
    {
        byte[] encoded = Convert.FromHexString("83010203");
        Assert.Equal(CborDecodeResult.Success, CborValidation.TryValidate(encoded,
            new CborReaderOptions(maxItems: 4, maxEncodedLength: 4)));
        Assert.Equal(CborDecodeResult.LimitExceeded, CborValidation.TryValidate(encoded, new CborReaderOptions(maxItems: 3)));
        Assert.Equal(CborDecodeResult.LimitExceeded, CborValidation.TryValidate(encoded, new CborReaderOptions(maxEncodedLength: 3)));
        Assert.Equal(CborDecodeResult.Success, CborValidation.TryValidate([0x9f, 0xff],
            new CborReaderOptions(maxItems: 1, maxEncodedLength: 2)));
    }

    [Theory]
    [InlineData("1800")]
    [InlineData("790000")]
    [InlineData("d80000")]
    [InlineData("fa3f800000")]
    [InlineData("fb3ff0000000000000")]
    [InlineData("f97c01")]
    public void PreferredWidthPolicyRejectsWiderValuesAndNonCanonicalNaNs(string hex)
    {
        byte[] input = Convert.FromHexString(hex);
        Assert.Equal(CborDecodeResult.Success, CborValidation.TryValidate(input));
        Assert.Equal(CborDecodeResult.EncodingPolicyViolation, CborValidation.TryValidate(input,
            new CborReaderOptions(requirePreferredEncoding: true)));
    }

    [Fact]
    public void IndefiniteLengthAndPreferredWidthAreSeparatePolicies()
    {
        byte[] encoded = Convert.FromHexString("9f00ff");
        Assert.Equal(CborDecodeResult.Success, CborValidation.TryValidate(encoded, new CborReaderOptions(requirePreferredEncoding: true)));
        Assert.Equal(CborDecodeResult.EncodingPolicyViolation, CborValidation.TryValidate(encoded, new CborReaderOptions(allowIndefiniteLength: false)));
    }

    [Fact]
    public void OptionsRejectImpossibleResourceBounds()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CborReaderOptions(maxDepth: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CborReaderOptions(maxDepth: 1025));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CborReaderOptions(maxItems: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CborReaderOptions(maxEncodedLength: 0));
    }
}
