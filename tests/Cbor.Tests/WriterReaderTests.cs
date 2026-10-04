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

    [Theory]
    [InlineData("1BFFFFFFFFFFFFFFFF")]
    [InlineData("3BFFFFFFFFFFFFFFFF")]
    [InlineData("5800")]
    [InlineData("7800")]
    [InlineData("9800")]
    [InlineData("B800")]
    [InlineData("D81800")]
    [InlineData("F820")]
    [InlineData("F93C00")]
    [InlineData("FA3F800000")]
    [InlineData("FB3FF0000000000000")]
    public void CompleteExtendedPrefixesReachCustomFormattersAcrossSeams(string hex)
    {
        byte[] bytes = Convert.FromHexString(hex);
        var factory = new PrefixProbeFactory();
        var options = new CborSerializerOptions(factory);
        CborSerializer.Deserialize<PrefixProbe>(bytes, options);
        Assert.Equal(1, factory.Reads);
        var sequence = Cbor.Testing.IntegerHarness.Fragment(bytes);
        CborSerializer.Deserialize<PrefixProbe>(in sequence, options);
        Assert.Equal(2, factory.Reads);
    }

    [Theory]
    [InlineData("1C", false)]
    [InlineData("3F", false)]
    [InlineData("DF", false)]
    [InlineData("FC", false)]
    [InlineData("FF", false)]
    [InlineData("F818", false)]
    [InlineData("18", true)]
    [InlineData("1B000000", true)]
    [InlineData("FB000000", true)]
    public void MalformedExtendedPrefixesFailBeforeCustomFormatters(string hex, bool truncated)
    {
        byte[] bytes = Convert.FromHexString(hex);
        var factory = new PrefixProbeFactory();
        var options = new CborSerializerOptions(factory);
        if (truncated)
        {
            Assert.Throws<EndOfStreamException>(() => CborSerializer.Deserialize<PrefixProbe>(bytes, options));
        }
        else
        {
            Assert.Throws<InvalidDataException>(() => CborSerializer.Deserialize<PrefixProbe>(bytes, options));
        }
        Assert.Equal(0, factory.Reads);
    }

    [Theory]
    [InlineData("1B0000000000000001")]
    [InlineData("3800")]
    [InlineData("5800")]
    [InlineData("7800")]
    [InlineData("9800")]
    [InlineData("B800")]
    [InlineData("D80100")]
    [InlineData("FA3F800000")]
    [InlineData("FB3FF0000000000000")]
    public void PreferredPrefixPolicyStillPrecedesCustomFormatters(string hex)
    {
        var factory = new PrefixProbeFactory();
        var options = new CborSerializerOptions(factory, new CborReaderOptions(requirePreferredEncoding: true));
        Assert.Throws<InvalidDataException>(() => CborSerializer.Deserialize<PrefixProbe>(Convert.FromHexString(hex), options));
        Assert.Equal(0, factory.Reads);
    }

    [Fact]
    public void BuiltinIntegerArrayBatchesMatchRfcTokensAndStayInsideExactWindows()
    {
        CheckIntegerArrays<byte>([0, 23, 24, byte.MaxValue], ["00", "17", "1818", "18FF"]);
        CheckIntegerArrays<sbyte>([sbyte.MinValue, -25, -24, -1, 0, 23, 24, sbyte.MaxValue],
            ["387F", "3818", "37", "20", "00", "17", "1818", "187F"]);
        CheckIntegerArrays<short>([short.MinValue, -257, -256, -25, -24, 0, 256, short.MaxValue],
            ["397FFF", "390100", "38FF", "3818", "37", "00", "190100", "197FFF"]);
        CheckIntegerArrays<ushort>([0, 23, 24, 255, 256, ushort.MaxValue],
            ["00", "17", "1818", "18FF", "190100", "19FFFF"]);
        CheckIntegerArrays<int>([int.MinValue, -65537, -65536, -257, -256, -25, -24, 0, 23, 24, 255, 256, 65535, 65536, int.MaxValue],
            ["3A7FFFFFFF", "3A00010000", "39FFFF", "390100", "38FF", "3818", "37", "00", "17", "1818", "18FF", "190100", "19FFFF", "1A00010000", "1A7FFFFFFF"]);
        CheckIntegerArrays<uint>([0, 23, 24, 255, 256, 65535, 65536, uint.MaxValue],
            ["00", "17", "1818", "18FF", "190100", "19FFFF", "1A00010000", "1AFFFFFFFF"]);
        CheckIntegerArrays<long>([long.MinValue, -4294967297, -4294967296, -1, 0, long.MaxValue],
            ["3B7FFFFFFFFFFFFFFF", "3B0000000100000000", "3AFFFFFFFF", "20", "00", "1B7FFFFFFFFFFFFFFF"]);
        CheckIntegerArrays<ulong>([0, 23, 255, 256, 65536, uint.MaxValue, (ulong)uint.MaxValue + 1, ulong.MaxValue],
            ["00", "17", "18FF", "190100", "1A00010000", "1AFFFFFFFF", "1B0000000100000000", "1BFFFFFFFFFFFFFFFF"]);
    }

    [Theory]
    [InlineData(true, "841818190100")]
    [InlineData(false, "8418181901001A7FFFFFFF")]
    public unsafe void IntegerArrayBatchFailuresPublishTheScalarPrefix(bool itemLimit, string prefix)
    {
        var options = new CborSerializerOptions(new IntegerArrayFactory<int>(),
            itemLimit ? new CborReaderOptions(maxItems: 3) : new CborReaderOptions(maxEncodedLength: 6));
        byte[] storage = new byte[64];
        fixed (byte* pointer = storage)
        {
            var buffer = new CompatibleSpanWriteBuffer(pointer, storage.Length);
            try
            {
                try
                {
                    CborSerializer.Serialize(ref buffer, new[] { 24, 256, int.MaxValue, 0 }, options);
                    Assert.Fail("Exceeded budget accepted.");
                }
                catch (InvalidDataException)
                {
                    byte[] expected = Convert.FromHexString(prefix);
                    Assert.Equal(expected.Length, buffer.BytesWritten);
                    Assert.Equal(expected, storage.AsSpan(0, expected.Length).ToArray());
                }
            }
            finally { buffer.Dispose(); }
        }
    }

    [Fact]
    public void IntegerArrayItemBudgetFailsBeforeAcquiringElementStorage()
    {
        var options = new CborSerializerOptions(new IntegerArrayFactory<int>(), new CborReaderOptions(maxItems: 1));
        int[] values = [24, 256];
        byte[] storage = new byte[64];
        var buffer = new BudgetGuardWriteBuffer(storage);
        try
        {
            try
            {
                CborSerializer.Serialize(ref buffer, values, options);
                Assert.Fail("Exceeded item budget accepted.");
            }
            catch (InvalidDataException)
            {
                Assert.Equal(1, buffer.BytesWritten);
                Assert.Equal(0x82, storage[0]);
            }
        }
        finally { buffer.Dispose(); }
    }

    private struct BudgetGuardWriteBuffer(byte[] storage) : IWriteBuffer
    {
        private int written;
        public readonly long BytesWritten => written;
        public readonly Span<byte> GetSpan(int sizeHint = 0)
        {
            if (written != 0) { throw new InvalidOperationException("Element storage was acquired after the item budget was exhausted."); }
            return storage;
        }
        public void Advance(int count) => written += count;
        public readonly void Flush() { }
        public readonly void Dispose() { }
    }

    private static unsafe void CheckIntegerArrays<T>(T[] pattern, string[] tokens)
    {
        const byte Sentinel = 0xa5;
        const int GuardLength = 7;
        int[] counts = [0, 1, 2, 8, 31, 32, 33, 1023, 1024, 1025];
        var options = new CborSerializerOptions(new IntegerArrayFactory<T>());
        foreach (int count in counts)
        {
            var values = new T[count];
            byte[] header = new byte[CborPrimitives.MaxHeaderLength];
            CborPrimitives.TryWriteHeader(header, CborMajorType.Array, (ulong)count, out int length);
            var expected = new List<byte>(header.AsSpan(0, length).ToArray());
            for (int i = 0; i < count; i++)
            {
                values[i] = pattern[i % pattern.Length];
                expected.AddRange(Convert.FromHexString(tokens[i % pattern.Length]));
            }
            byte[] bytes = expected.ToArray();
            Assert.Equal(bytes, CborSerializer.Serialize(values, options));
            Assert.Equal(values, CborSerializer.Deserialize<T[]>(bytes, options));
            byte[] guarded = new byte[bytes.Length + GuardLength * 2];
            Array.Fill(guarded, Sentinel);
            fixed (byte* pointer = guarded)
            {
                var buffer = new CompatibleSpanWriteBuffer(pointer + GuardLength, bytes.Length);
                try
                {
                    CborSerializer.Serialize(ref buffer, values, options);
                    Assert.Equal(bytes.Length, buffer.BytesWritten);
                }
                finally { buffer.Dispose(); }
            }
            Assert.Equal(bytes, guarded.AsSpan(GuardLength, bytes.Length).ToArray());
            Assert.All(guarded.AsSpan(0, GuardLength).ToArray(), static item => Assert.Equal(Sentinel, item));
            Assert.All(guarded.AsSpan(GuardLength + bytes.Length).ToArray(), static item => Assert.Equal(Sentinel, item));
        }
    }

    private sealed class IntegerArrayFactory<T> : TestFactory
    {
        protected override object? Create<W, R>(Type valueType) => valueType == typeof(T[]) ? new CborArrayFormatter<W, R, T>() : null;
    }

    private sealed class PrefixProbe;

    private sealed class PrefixProbeFactory : TestFactory
    {
        internal int Reads { get; private set; }
        protected override object? Create<W, R>(Type valueType) => valueType == typeof(PrefixProbe) ? new Formatter<W, R>(this) : null;

        private sealed class Formatter<W, R>(PrefixProbeFactory owner) : ICborFormatter<W, R, PrefixProbe>
            where W : struct, IWriteBuffer
#if NET9_0_OR_GREATER
            , allows ref struct
#endif
            where R : struct, IReadBuffer
#if NET9_0_OR_GREATER
            , allows ref struct
#endif
        {
            public void Initialize(CborFormatterResolver resolver) { }
            public void Serialize(ref W buffer, ref CborSerializationContext context, PrefixProbe value) => throw new NotSupportedException();
            public PrefixProbe Deserialize(ref R buffer, ref CborDeserializationContext context)
            {
                owner.Reads++;
                buffer.Advance((int)buffer.BytesRemaining);
                return new PrefixProbe();
            }
        }
    }
}
