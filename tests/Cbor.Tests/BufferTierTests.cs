using System.Buffers;
using System.Runtime.Versioning;
using Cbor.Testing;

namespace Cbor.Tests;

public sealed class BufferTierTests
{
    [Theory]
    [InlineData(0UL)]
    [InlineData(23UL)]
    [InlineData(24UL)]
    [InlineData(255UL)]
    [InlineData(256UL)]
    [InlineData(65535UL)]
    [InlineData(65536UL)]
    [InlineData(4294967295UL)]
    [InlineData(4294967296UL)]
    [InlineData(ulong.MaxValue)]
    public void PooledAndWriterDestinationsAgree(ulong value)
    {
        byte[] pooled = IntegerHarness.Encode(value);
        var output = new ArrayBufferWriter<byte>();
        IntegerHarness.EncodeTo(output, value);

        Assert.Equal(pooled, output.WrittenSpan.ToArray());
        Assert.Equal(value, IntegerHarness.Decode(pooled));
    }

    [Fact]
    public void SegmentedInputsReadAcrossEverySeam()
    {
        const ulong value = ulong.MaxValue;
        byte[] encoded = IntegerHarness.Encode(value);
        for (int offset = 0; offset <= encoded.Length; offset++)
        {
            ReadOnlySequence<byte> source = IntegerHarness.Split(encoded, offset);
            Assert.Equal(value, IntegerHarness.Decode(in source));
        }
    }

    [Fact]
    public void ConsumerSelectsExpectedRuntimeAsset()
    {
        var attribute = (TargetFrameworkAttribute?)Attribute.GetCustomAttribute(
            typeof(CborPrimitives).Assembly, typeof(TargetFrameworkAttribute));
#if CBOR_NETSTANDARD20
        const string expected = ".NETStandard,Version=v2.0";
#elif CBOR_NETSTANDARD21
        const string expected = ".NETStandard,Version=v2.1";
#elif NET10_0_OR_GREATER
        const string expected = ".NETCoreApp,Version=v10.0";
#elif NET9_0_OR_GREATER
        const string expected = ".NETCoreApp,Version=v9.0";
#elif NET8_0_OR_GREATER
        const string expected = ".NETCoreApp,Version=v8.0";
#else
        const string expected = ".NETStandard,Version=v2.1";
#endif
        Assert.NotNull(attribute);
        Assert.Equal(expected, attribute.FrameworkName);
    }
}
