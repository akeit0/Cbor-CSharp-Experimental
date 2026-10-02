using System.Buffers;
using Cbor.Testing;
using SerializerFoundation;

namespace Cbor.Tests.Robustness;

public sealed class UnsignedIntegerRobustnessTests
{
    [Theory]
    [InlineData(24UL)]
    [InlineData(256UL)]
    [InlineData(65536UL)]
    [InlineData(4294967296UL)]
    [InlineData(ulong.MaxValue)]
    public void EveryTruncatedPrefixFailsWithoutConsumingInput(ulong value)
    {
        byte[] encoded = IntegerHarness.Encode(value);
        for (int length = 0; length < encoded.Length; length++)
        {
            byte[] prefix = encoded.AsSpan(0, length).ToArray();
            var buffer = new CompatibleReadOnlySequenceReadBuffer(new ReadOnlySequence<byte>(prefix));
            try
            {
                // ref buffers must not be captured or copied into Assert.Throws lambdas.
                try
                {
                    buffer.ReadUInt64();
                    Assert.Fail("A truncated integer was accepted.");
                }
                catch (EndOfStreamException)
                {
                    Assert.Equal(0, buffer.BytesConsumed);
                }
            }
            finally
            {
                buffer.Dispose();
            }
        }
    }

    [Fact]
    public void ReservedHeadersAndOtherMajorTypesAreRejected()
    {
        for (int header = 0x1c; header <= byte.MaxValue; header++)
        {
            byte[] input = [(byte)header, 0, 0, 0, 0, 0, 0, 0, 0];
            Assert.Throws<InvalidDataException>(() => IntegerHarness.Decode(input));
        }
    }

    [Fact]
    public void SingleValueHarnessRejectsTrailingData()
    {
        Assert.Throws<InvalidDataException>(() => IntegerHarness.Decode([0x00, 0x01]));
    }

    [Fact]
    public void ReproducibleGeneratedValuesAgreeWithSegmentedInput()
    {
        var random = new Random(8949);
        byte[] bytes = new byte[8];
        for (int iteration = 0; iteration < 1000; iteration++)
        {
            random.NextBytes(bytes);
            ulong value = System.Buffers.Binary.BinaryPrimitives.ReadUInt64BigEndian(bytes);
            byte[] encoded = IntegerHarness.Encode(value);
            int offset = random.Next(encoded.Length + 1);
            ReadOnlySequence<byte> source = IntegerHarness.Split(encoded, offset);
            Assert.Equal(value, IntegerHarness.Decode(in source));
        }
    }
}
