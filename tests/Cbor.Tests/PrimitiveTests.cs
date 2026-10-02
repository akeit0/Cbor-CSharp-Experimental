using Cbor.Testing;
using SerializerFoundation;

namespace Cbor.Tests;

public sealed class PrimitiveTests
{
    [Fact]
    public void SpecializedScalarReadersPreserveHeaderGrammarAndFailureConsumption()
    {
        byte[] token = new byte[9];
        foreach (byte fill in new byte[] { 0, 31, 32, 255 })
        {
            Array.Fill(token, fill);
            for (int initial = 0; initial <= 255; initial++)
            {
                token[0] = (byte)initial;
                for (int length = 0; length <= token.Length; length++)
                {
                    var source = token.AsSpan(0, length);
                    var grammar = CborPrimitives.TryReadHeader(source, out var header);
                    var unsigned = grammar != CborDecodeResult.Success ? grammar :
                        header.MajorType == CborMajorType.UnsignedInteger ? CborDecodeResult.Success : CborDecodeResult.TypeMismatch;
                    Assert.Equal(unsigned, CborPrimitives.TryReadUInt64(source, out ulong unsignedValue, out int consumed));
                    Assert.Equal(unsigned == CborDecodeResult.Success ? header.EncodedLength : 0, consumed);
                    if (unsigned == CborDecodeResult.Success)
                    {
                        Assert.Equal(header.Argument, unsignedValue);
                    }

                    var signed = grammar != CborDecodeResult.Success ? grammar :
                        header.MajorType is not (CborMajorType.UnsignedInteger or CborMajorType.NegativeInteger) ? CborDecodeResult.TypeMismatch :
                        header.Argument > long.MaxValue ? CborDecodeResult.Overflow : CborDecodeResult.Success;
                    Assert.Equal(signed, CborPrimitives.TryReadInt64(source, out long signedValue, out consumed));
                    Assert.Equal(signed == CborDecodeResult.Success ? header.EncodedLength : 0, consumed);
                    if (signed == CborDecodeResult.Success)
                    {
                        Assert.Equal(header.MajorType == CborMajorType.UnsignedInteger ? (long)header.Argument : ~(long)header.Argument, signedValue);
                    }

                    var boolean = grammar != CborDecodeResult.Success ? grammar :
                        initial is 0xf4 or 0xf5 ? CborDecodeResult.Success : CborDecodeResult.TypeMismatch;
                    Assert.Equal(boolean, CborPrimitives.TryReadBoolean(source, out bool booleanValue, out consumed));
                    Assert.Equal(boolean == CborDecodeResult.Success ? 1 : 0, consumed);
                    if (boolean == CborDecodeResult.Success)
                    {
                        Assert.Equal(initial == 0xf5, booleanValue);
                    }

                    var floating = grammar != CborDecodeResult.Success ? grammar :
                        initial is >= 0xf9 and <= 0xfb ? CborDecodeResult.Success : CborDecodeResult.TypeMismatch;
                    Assert.Equal(floating, CborPrimitives.TryReadDouble(source, out _, out consumed));
                    Assert.Equal(floating == CborDecodeResult.Success ? header.EncodedLength : 0, consumed);
                }
            }
        }
    }

    [Theory]
    [InlineData(long.MinValue, "3b7fffffffffffffff")]
    [InlineData(-1000L, "3903e7")]
    [InlineData(-24L, "37")]
    [InlineData(-25L, "3818")]
    [InlineData(-1L, "20")]
    [InlineData(0L, "00")]
    [InlineData(long.MaxValue, "1b7fffffffffffffff")]
    public void SignedIntegerFixtures(long value, string hex)
    {
        byte[] actual = new byte[9];
        Assert.True(CborPrimitives.TryWriteInt64(actual, value, out int written));
        Assert.Equal(Convert.FromHexString(hex), actual.AsSpan(0, written).ToArray());
        Assert.Equal(CborDecodeResult.Success, CborPrimitives.TryReadInt64(actual.AsSpan(0, written), out long decoded, out int consumed));
        Assert.Equal(value, decoded);
        Assert.Equal(written, consumed);
    }

    [Fact]
    public void FullNegativeArgumentDoesNotNarrowIntoInt64()
    {
        byte[] input = Convert.FromHexString("3bffffffffffffffff");
        Assert.Equal(CborDecodeResult.Overflow, CborPrimitives.TryReadInt64(input, out _, out int consumed));
        Assert.Equal(0, consumed);
        Assert.Equal(CborDecodeResult.Success, CborPrimitives.TryReadNegativeIntegerArgument(input, out ulong argument, out consumed));
        Assert.Equal(ulong.MaxValue, argument);
        Assert.Equal(9, consumed);
    }

    [Fact]
    public void ShortWritesAreAtomic()
    {
        byte[] destination = Enumerable.Repeat((byte)0xa5, 8).ToArray();
        Assert.False(CborPrimitives.TryWriteUInt64(destination, ulong.MaxValue, out int written));
        Assert.Equal(0, written);
        Assert.False(CborPrimitives.TryWriteDoublePrecision(destination, 1.1, out written));
        Assert.Equal(0, written);
        Assert.All(destination, item => Assert.Equal(0xa5, item));
    }

    [Fact]
    public void StringSpanWritesAreAtomicAndHandleOverlap()
    {
        byte[] destination = [1, 2, 3, 4, 0];
        Assert.True(CborPrimitives.TryWriteByteString(destination, destination.AsSpan(0, 4), out int written));
        Assert.Equal(5, written);
        Assert.Equal(new byte[] { 0x44, 1, 2, 3, 4 }, destination);

        byte[] shortDestination = [0xa5, 0xa5, 0xa5];
        Assert.False(CborPrimitives.TryWriteByteString(shortDestination, new byte[] { 1, 2, 3 }, out written));
        Assert.Equal(0, written);
        Assert.All(shortDestination, value => Assert.Equal(0xa5, value));
    }

    [Fact]
    public void ReservedSimpleValuesAreNeverEmittedAndNullIsDistinctFromUndefined()
    {
        byte[] destination = new byte[2];
        for (int value = 0; value <= byte.MaxValue; value++)
        {
            if (value is >= 24 and < 32)
            {
                Assert.Throws<ArgumentOutOfRangeException>(() =>
                    CborPrimitives.TryWriteSimpleValue(destination, (byte)value, out _));
                continue;
            }

            Assert.True(CborPrimitives.TryWriteSimpleValue(destination, (byte)value, out int written));
            Assert.Equal(CborDecodeResult.Success,
                CborPrimitives.TryReadSimpleValue(destination.AsSpan(0, written), out byte decoded, out int consumed));
            Assert.Equal(value, decoded);
            Assert.Equal(written, consumed);
        }

        CborPrimitives.TryWriteNull(destination, out _);
        Assert.Equal(0xf6, destination[0]);
        CborPrimitives.TryWriteUndefined(destination, out _);
        Assert.Equal(0xf7, destination[0]);
    }

    [Fact]
    public void EveryHalfBitPatternAgreesWithTheRuntimeAndPreferredRoundTrips()
    {
        byte[] input = new byte[3];
        byte[] preferred = new byte[9];
        for (int raw = 0; raw <= ushort.MaxValue; raw++)
        {
            input[0] = 0xf9;
            System.Buffers.Binary.BinaryPrimitives.WriteUInt16BigEndian(input.AsSpan(1), (ushort)raw);
            double expected = (double)BitConverter.UInt16BitsToHalf((ushort)raw);
            Assert.Equal(CborDecodeResult.Success, CborPrimitives.TryReadDouble(input, out double actual, out int consumed));
            Assert.Equal(3, consumed);
            if (double.IsNaN(expected))
            {
                Assert.True(double.IsNaN(actual));
            }
            else
            {
                Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual));
            }

            Assert.True(CborPrimitives.TryWriteDouble(preferred, actual, out int written));
            Assert.Equal(3, written);
            if (double.IsNaN(actual))
            {
                Assert.Equal(Convert.FromHexString("f97e00"), preferred.AsSpan(0, written).ToArray());
            }
            else
            {
                Assert.Equal(input, preferred.AsSpan(0, written).ToArray());
            }
        }
    }

    [Fact]
    public void EveryInitialByteHasTheExpectedHeaderGrammar()
    {
        byte[] token = new byte[9];
        for (int initial = 0; initial <= 255; initial++)
        {
            Array.Clear(token);
            token[0] = (byte)initial;
            token[1] = 32; // Legal extended simple value.
            int major = initial >> 5;
            int additional = initial & 31;
            bool invalid = additional is >= 28 and <= 30 || (additional == 31 && major is 0 or 1 or 6);
            Assert.Equal(invalid ? CborDecodeResult.InvalidData : CborDecodeResult.Success,
                CborPrimitives.TryReadHeader(token, out var header));
            if (!invalid)
            {
                Assert.Equal((CborMajorType)major, header.MajorType);
                Assert.Equal(additional, header.AdditionalInformation);
            }
        }
    }

#if NET9_0_OR_GREATER
    [Fact]
    public void FixedDestinationsRequireOnlyTheActualEncodedSize()
    {
        Span<byte> singleByte = stackalloc byte[1];
        var buffer = new SpanWriteBuffer(singleByte);
        try
        {
            buffer.WriteUInt64(0);
            Assert.Equal(1, buffer.BytesWritten);
            Assert.Equal(0, singleByte[0]);
        }
        finally
        {
            buffer.Dispose();
        }

        Span<byte> halfBytes = stackalloc byte[3];
        var halfBuffer = new SpanWriteBuffer(halfBytes);
        try
        {
            halfBuffer.WriteDouble(-0.0);
            Assert.Equal(Convert.FromHexString("f98000"), halfBytes.ToArray());
        }
        finally
        {
            halfBuffer.Dispose();
        }
    }
#endif

    [Fact]
    public void HeaderAndScalarErrorsDoNotConsumeBufferInput()
    {
        byte[][] inputs = [Convert.FromHexString("1bffffffffffffffff"), [0xf6], [0x19, 0x01]];
        foreach (byte[] input in inputs)
        {
            var buffer = new CompatibleReadOnlySequenceReadBuffer(new System.Buffers.ReadOnlySequence<byte>(input));
            try
            {
                try
                {
                    buffer.ReadInt64();
                    Assert.Fail("An invalid Int64 was accepted.");
                }
                catch (Exception error) when (error is OverflowException or InvalidDataException or EndOfStreamException)
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
}
