using System.Buffers;
using System.Runtime.InteropServices;
using Cbor.Internal;
using SerializerFoundation;

namespace Cbor.Tests;

public sealed class WriterReaderTests
{
    [Fact]
    public unsafe void UnsafeHeaderStoresStayInsideTheReservedWindowAndMatchExactSpanWrites()
    {
        var arguments = new List<ulong> { 0, 23, 24, ulong.MaxValue };
        for (int bit = 0; bit < 64; bit++)
        {
            ulong value = 1UL << bit;
            arguments.Add(value - 1);
            arguments.Add(value);
            arguments.Add(value + 1);
        }

        byte[] expected = new byte[9];
        byte[] guarded = new byte[41];
        foreach (ulong argument in arguments)
        {
            for (int major = 0; major <= 6; major++)
            {
                CborPrimitives.TryWriteHeader(expected, (CborMajorType)major, argument, out int length);
                for (int offset = 0; offset < 16; offset++)
                {
                    Array.Fill(guarded, (byte)0xa5);
                    int actual = CborTokenEncoder.UnsafeWriteHeader(ref MemoryMarshal.GetReference(guarded.AsSpan(offset, 9)), (CborMajorType)major, argument);
                    Assert.Equal(length, actual);
                    Assert.Equal(expected.AsSpan(0, length).ToArray(), guarded.AsSpan(offset, length).ToArray());
                    Assert.All(guarded.AsSpan(0, offset).ToArray(), static value => Assert.Equal(0xa5, value));
                    Assert.All(guarded.AsSpan(offset + 9).ToArray(), static value => Assert.Equal(0xa5, value));

                    Array.Fill(guarded, (byte)0xa5);
                    fixed (byte* pointer = guarded)
                    {
                        var writer = new CompatibleSpanWriteBuffer(pointer + offset, length);
                        try
                        {
                            writer.WriteHeader((CborMajorType)major, argument);
                            Assert.Equal(length, writer.BytesWritten);
                        }
                        finally { writer.Dispose(); }
                    }

                    Assert.Equal(expected.AsSpan(0, length).ToArray(), guarded.AsSpan(offset, length).ToArray());
                    Assert.All(guarded.AsSpan(0, offset).ToArray(), static value => Assert.Equal(0xa5, value));
                    Assert.All(guarded.AsSpan(offset + length).ToArray(), static value => Assert.Equal(0xa5, value));
                }
            }
        }
    }

    [Fact]
    public unsafe void UnsafePreferredFloatsMatchTheSpanWriterAndStayInsideTheirWindow()
    {
        var random = new Random(20261002);
        byte[] raw = new byte[8];
        byte[] expected = new byte[9];
        byte[] guarded = new byte[25];
        for (int sample = 0; sample < 10_000; sample++)
        {
            random.NextBytes(raw);
            double value = BitConverter.Int64BitsToDouble(BitConverter.ToInt64(raw));
            CborPrimitives.TryWriteDouble(expected, value, out int length);
            Array.Fill(guarded, (byte)0xa5);
            fixed (byte* pointer = guarded)
            {
                var writer = new CompatibleSpanWriteBuffer(pointer + 7, length);
                try
                {
                    writer.WriteDouble(value);
                    Assert.Equal(length, writer.BytesWritten);
                }
                finally { writer.Dispose(); }
            }
            Assert.Equal(expected.AsSpan(0, length).ToArray(), guarded.AsSpan(7, length).ToArray());
            Assert.All(guarded.AsSpan(0, 7).ToArray(), static item => Assert.Equal(0xa5, item));
            Assert.All(guarded.AsSpan(7 + length).ToArray(), static item => Assert.Equal(0xa5, item));
        }

        // Random Double bits rarely hit exactly representable half values; cover them exhaustively.
        for (int bits = 0; bits <= ushort.MaxValue; bits++)
        {
            double value = (double)BitConverter.UInt16BitsToHalf((ushort)bits);
            CborPrimitives.TryWriteDouble(expected, value, out int length);
            fixed (byte* pointer = guarded)
            {
                var writer = new CompatibleSpanWriteBuffer(pointer + 7, length);
                try
                {
                    writer.WriteDouble(value);
                    Assert.Equal(length, writer.BytesWritten);
                }
                finally { writer.Dispose(); }
            }
            Assert.Equal(expected.AsSpan(0, length).ToArray(), guarded.AsSpan(7, length).ToArray());
        }
    }

    [Theory]
    [InlineData("F93E00", 1.5)]
    [InlineData("FA47C35000", 100000.0)]
    [InlineData("FB3FF199999999999A", 1.1)]
    [InlineData("F98000", -0.0)]
    public void SpecializedFloatingReadsHandleEverySeamAndTruncation(string hex, double expected)
    {
        byte[] bytes = Convert.FromHexString(hex);
        for (int split = 0; split <= bytes.Length; split++)
        {
            var reader = new CompatibleReadOnlySequenceReadBuffer(Cbor.Testing.IntegerHarness.Split(bytes, split));
            try
            {
                Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(reader.ReadDouble()));
                Assert.Equal(bytes.Length, reader.BytesConsumed);
            }
            finally { reader.Dispose(); }
        }

        for (int length = 0; length < bytes.Length; length++)
        {
            var reader = new CompatibleReadOnlySequenceReadBuffer(Cbor.Testing.IntegerHarness.Fragment(bytes.AsSpan(0, length).ToArray()));
            try
            {
                try { reader.ReadDouble(); Assert.Fail("Truncated float accepted."); }
                catch (EndOfStreamException) { Assert.Equal(0, reader.BytesConsumed); }
            }
            finally { reader.Dispose(); }
        }
    }

    [Theory]
    [InlineData("17", 23L)]
    [InlineData("1818", 24L)]
    [InlineData("190100", 256L)]
    [InlineData("1A00010000", 65536L)]
    [InlineData("1B0000000100000000", 4294967296L)]
    [InlineData("37", -24L)]
    [InlineData("3818", -25L)]
    [InlineData("3B7FFFFFFFFFFFFFFF", long.MinValue)]
    public void SpecializedIntegerPathsWorkAcrossEverySeamAndDoNotConsumeTruncation(string hex, long expected)
    {
        byte[] bytes = Convert.FromHexString(hex);
        for (int split = 0; split <= bytes.Length; split++)
        {
            var reader = new CompatibleReadOnlySequenceReadBuffer(Cbor.Testing.IntegerHarness.Split(bytes, split));
            try
            {
                Assert.Equal(expected, reader.ReadInt64());
                Assert.Equal(bytes.Length, reader.BytesConsumed);
            }
            finally { reader.Dispose(); }
        }

        for (int length = 0; length < bytes.Length; length++)
        {
            var reader = new CompatibleReadOnlySequenceReadBuffer(Cbor.Testing.IntegerHarness.Fragment(bytes.AsSpan(0, length).ToArray()));
            try
            {
                try { reader.ReadInt64(); Assert.Fail("Truncated integer accepted."); }
                catch (EndOfStreamException) { Assert.Equal(0, reader.BytesConsumed); }
            }
            finally { reader.Dispose(); }
        }
    }

    [Fact]
    public void CompoundValueExercisesAllMajorTypesAndHeaderUnits()
    {
        var output = new ArrayBufferWriter<byte>();
        var writer = new CompatibleBufferWriterWriteBuffer(output);
        try
        {
            writer.WriteArrayHeader(8);
            writer.WriteUInt64(24);
            writer.WriteInt64(-1000);
            writer.WriteByteString([1, 2, 3, 4]);
            writer.WriteTextString("水");
            writer.WriteArrayHeader(0);
            writer.WriteMapHeader(1);
            writer.WriteTextString("a");
            writer.WriteBoolean(true);
            writer.WriteTag(32);
            writer.WriteTextString("https://example.com");
            writer.WriteUndefined();
            writer.Flush();
        }
        finally
        {
            writer.Dispose();
        }

        byte[] encoded = output.WrittenSpan.ToArray();
        Assert.Equal(CborDecodeResult.Success, CborValidation.TryValidate(encoded));
        var reader = new CompatibleReadOnlySequenceReadBuffer(new ReadOnlySequence<byte>(encoded));
        try
        {
            Assert.Equal(8UL, reader.ReadArrayHeader());
            Assert.Equal(24UL, reader.ReadUInt64());
            Assert.Equal(-1000, reader.ReadInt64());
            Assert.Equal(new byte[] { 1, 2, 3, 4 }, reader.ReadByteString());
            Assert.Equal("水", reader.ReadTextString());
            Assert.Equal(0UL, reader.ReadArrayHeader());
            Assert.Equal(1UL, reader.ReadMapHeader());
            Assert.Equal("a", reader.ReadTextString());
            Assert.True(reader.ReadBoolean());
            Assert.Equal(32UL, reader.ReadTag());
            Assert.Equal("https://example.com", reader.ReadTextString());
            reader.ReadUndefined();
            Assert.Equal(0, reader.BytesRemaining);
        }
        finally
        {
            reader.Dispose();
        }
    }

    [Fact]
    public void IndefiniteChunksProduceExactRfcEncoding()
    {
        var output = new ArrayBufferWriter<byte>();
        var writer = new CompatibleBufferWriterWriteBuffer(output);
        try
        {
            writer.WriteStartIndefiniteTextString();
            writer.WriteTextString("strea");
            writer.WriteTextString("ming");
            writer.WriteBreak();
            writer.Flush();
        }
        finally
        {
            writer.Dispose();
        }

        Assert.Equal(Convert.FromHexString("7f657374726561646d696e67ff"), output.WrittenSpan.ToArray());
    }

    [Fact]
    public void InvalidUtf16AndUtf8AreRejectedBeforePublishingAHeader()
    {
        var output = new ArrayBufferWriter<byte>();
        var writer = new CompatibleBufferWriterWriteBuffer(output);
        try
        {
            try
            {
                writer.WriteTextString("\ud800");
                Assert.Fail("Unpaired surrogate accepted.");
            }
            catch (System.Text.EncoderFallbackException)
            {
                Assert.Equal(0, writer.BytesWritten);
            }

            try
            {
                writer.WriteTextStringUtf8([0xc0, 0x80]);
                Assert.Fail("Overlong UTF-8 accepted.");
            }
            catch (ArgumentException)
            {
                Assert.Equal(0, writer.BytesWritten);
            }
        }
        finally
        {
            writer.Dispose();
        }
    }

    [Fact]
    public void AllocationBoundsAndIncompletePayloadsDoNotConsumeTheirHeaders()
    {
        byte[][] inputs = [Convert.FromHexString("5bffffffffffffffff"), [0x43, 1, 2]];
        foreach (byte[] input in inputs)
        {
            var reader = new CompatibleReadOnlySequenceReadBuffer(new ReadOnlySequence<byte>(input));
            try
            {
                try
                {
                    reader.ReadByteString(maxLength: 32);
                    Assert.Fail("Invalid materialization succeeded.");
                }
                catch (Exception error) when (error is InvalidDataException or EndOfStreamException)
                {
                    Assert.Equal(0, reader.BytesConsumed);
                }
            }
            finally
            {
                reader.Dispose();
            }
        }
    }

    [Fact]
    public void FailedStreamingSkipReportsItsConsumedPrefix()
    {
        var reader = new CompatibleReadOnlySequenceReadBuffer(new ReadOnlySequence<byte>(new byte[] { 0x82, 0x00 }));
        try
        {
            Assert.Equal(CborDecodeResult.NeedMoreData, reader.TrySkipValue(out long consumed));
            Assert.Equal(2, consumed);
            Assert.Equal(consumed, reader.BytesConsumed);
        }
        finally
        {
            reader.Dispose();
        }
    }
}
