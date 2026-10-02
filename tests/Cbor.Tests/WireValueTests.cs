using System.Buffers;
using System.Numerics;
using Cbor.Testing;
using SerializerFoundation;

namespace Cbor.Tests;

public sealed class WireValueTests
{
    private static readonly CborSerializerOptions Options = new(WireValueFactory.Instance);

    [Fact]
    public void CompleteIntegerRangeUsesMajorTypesWithoutSignedNarrowing()
    {
        ulong[] arguments = [0, 23, 24, byte.MaxValue, ushort.MaxValue, uint.MaxValue, long.MaxValue, (ulong)long.MaxValue + 1, ulong.MaxValue];
        foreach (ulong argument in arguments)
        {
            AssertRoundTrip(new CborInteger(argument));
            AssertRoundTrip(CborInteger.FromNegativeArgument(argument));
        }
        Assert.Equal("3BFFFFFFFFFFFFFFFF", Convert.ToHexString(CborSerializer.Serialize(CborInteger.FromNegativeArgument(ulong.MaxValue))));
        Assert.Equal(-(BigInteger.One << 64), CborInteger.FromNegativeArgument(ulong.MaxValue).ToBigInteger());
        Assert.Equal(long.MinValue, CborInteger.FromInt64(long.MinValue).ToInt64());
        Assert.Throws<OverflowException>(() => new CborInteger(ulong.MaxValue).ToInt64());
        Assert.Throws<OverflowException>(() => CborInteger.FromNegativeArgument((ulong)long.MaxValue + 1).ToInt64());
        Assert.Throws<OverflowException>(() => CborInteger.FromNegativeArgument(0).ToUInt64());
        Assert.Equal(default, new CborInteger(0));
    }

    [Fact]
    public void EverySimpleValuePreservesNullUndefinedAndUnassignedValues()
    {
        for (int i = 0; i <= byte.MaxValue; i++)
        {
            if (i is >= 24 and <= 31)
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => new CborSimpleValue((byte)i));
                continue;
            }
            AssertRoundTrip(new CborSimpleValue((byte)i));
        }
        Assert.Equal("F6", Convert.ToHexString(CborSerializer.Serialize(CborSimpleValue.Null)));
        Assert.Equal("F7", Convert.ToHexString(CborSerializer.Serialize(CborSimpleValue.Undefined)));
        Assert.NotEqual(CborSimpleValue.Null, CborSimpleValue.Undefined);
        Assert.Equal(CborSimpleValue.False, CborSerializer.Deserialize<CborSimpleValue>([0xf4]));
        Assert.Equal(CborSimpleValue.True, CborSerializer.Deserialize<CborSimpleValue>([0xf5]));
        Assert.Throws<InvalidDataException>(() => CborSerializer.Deserialize<CborSimpleValue>([0xff]));
        Assert.Throws<InvalidDataException>(() => CborSerializer.Deserialize<CborSimpleValue>([0xf8, 0x17]));
        Assert.Throws<InvalidDataException>(() => CborSerializer.Deserialize<CborSimpleValue>([0xf9, 0, 0]));
    }

    [Fact]
    public void BignumsUsePreferredIntegersThenUnsignedBigEndianMagnitudes()
    {
        (BigInteger Value, string Hex)[] vectors =
        [
            (0, "00"), (-1, "20"), (ulong.MaxValue, "1BFFFFFFFFFFFFFFFF"),
            (-(BigInteger.One << 64), "3BFFFFFFFFFFFFFFFF"),
            (BigInteger.One << 64, "C249010000000000000000"),
            (-(BigInteger.One << 64) - 1, "C349010000000000000000"),
            ((BigInteger.One << 72) - 1, "C249FFFFFFFFFFFFFFFFFF"),
        ];
        foreach (var (value, hex) in vectors)
        {
            Assert.Equal(hex, Convert.ToHexString(CborSerializer.Serialize(value)));
            AssertRoundTrip(value);
        }
        foreach (int bits in new[] { 65, 127, 256, 2048, 4096 })
        {
            AssertRoundTrip((BigInteger.One << bits) - 1);
            AssertRoundTrip(-(BigInteger.One << bits));
        }
        Assert.Equal(BigInteger.Zero, CborSerializer.Deserialize<BigInteger>(Convert.FromHexString("C240")));
        Assert.Equal(BigInteger.MinusOne, CborSerializer.Deserialize<BigInteger>(Convert.FromHexString("C340")));
        Assert.Equal(new BigInteger(128), CborSerializer.Deserialize<BigInteger>(Convert.FromHexString("C2420080")));
        Assert.Equal(BigInteger.One << 64, CborSerializer.Deserialize<BigInteger>(Convert.FromHexString("C25F4401000000450000000000FF")));
    }

    [Theory]
    [InlineData("C24100")]
    [InlineData("C340")]
    [InlineData("C24A00010000000000000000")]
    [InlineData("D80249010000000000000000")]
    [InlineData("C25809010000000000000000")]
    public void PreferredBignumPolicyRejectsNonminimalTagsLengthsAndMagnitudes(string hex)
    {
        var preferred = new CborSerializerOptions(readerOptions: new(requirePreferredEncoding: true));
        Assert.Throws<InvalidDataException>(() => CborSerializer.Deserialize<BigInteger>(Convert.FromHexString(hex), preferred));
        Assert.Equal(BigInteger.One << 64, CborSerializer.Deserialize<BigInteger>(Convert.FromHexString("C249010000000000000000"), preferred));
    }

    [Theory]
    [InlineData("C4F6")]
    [InlineData("C2F6")]
    [InlineData("C28100")]
    [InlineData("C26178")]
    [InlineData("C25F6100FF")]
    public void BignumContractRejectsUnrelatedTagsAndInvalidContent(string hex)
        => Assert.Throws<InvalidDataException>(() => CborSerializer.Deserialize<BigInteger>(Convert.FromHexString(hex)));

    [Fact]
    public void TaggedValuesPreserveChainsNullAndFullTagRange()
    {
        var chain = new CborTagged<CborTagged<CborSimpleValue>>(ulong.MaxValue, new(1000, CborSimpleValue.Undefined));
        AssertRoundTrip(chain, Options);
        AssertRoundTrip(new CborTagged<string?>(24, null), Options);
        AssertRoundTrip(new CborTagged<BigInteger>(1000, BigInteger.One << 128), Options);
        byte[] indefinite = Convert.FromHexString("D8649F012002FF");
        var tagged = CborSerializer.Deserialize<CborTagged<List<CborInteger>>>(indefinite, Options);
        Assert.Equal(100UL, tagged.Tag);
        Assert.Equal(new[] { new CborInteger(1), CborInteger.FromNegativeArgument(0), new CborInteger(2) }, tagged.Value);
        Assert.Throws<InvalidDataException>(() => CborSerializer.Deserialize<CborTagged<int>>([1], Options));
        Assert.Throws<EndOfStreamException>(() => CborSerializer.Deserialize<CborTagged<int>>([0xc0], Options));
    }

    [Fact]
    public void TaggedAndBignumChildrenShareDepthItemsStringAndEncodingLimits()
    {
        var shallow = new CborSerializerOptions(WireValueFactory.Instance, new(maxDepth: 0));
        Assert.Throws<InvalidDataException>(() => CborSerializer.Serialize(new CborTagged<int>(0, 1), shallow));
        Assert.Throws<InvalidDataException>(() => CborSerializer.Deserialize<CborTagged<int>>([0xc0, 1], shallow));
        var oneItem = new CborSerializerOptions(WireValueFactory.Instance, new(maxItems: 1));
        Assert.Throws<InvalidDataException>(() => CborSerializer.Serialize(new CborTagged<int>(0, 1), oneItem));
        Assert.Throws<InvalidDataException>(() => CborSerializer.Deserialize<CborTagged<int>>([0xc0, 1], oneItem));
        Assert.Throws<InvalidDataException>(() => CborSerializer.Serialize(BigInteger.One << 64, oneItem));
        Assert.Throws<InvalidDataException>(() => CborSerializer.Deserialize<BigInteger>(Convert.FromHexString("C249010000000000000000"), oneItem));
        var shortString = new CborSerializerOptions(maxStringLength: 8);
        Assert.Throws<InvalidDataException>(() => CborSerializer.Serialize(BigInteger.One << 64, shortString));
        Assert.Throws<InvalidDataException>(() => CborSerializer.Deserialize<BigInteger>(Convert.FromHexString("C249010000000000000000"), shortString));
        var noIndefinite = new CborSerializerOptions(readerOptions: new(allowIndefiniteLength: false));
        Assert.Throws<InvalidDataException>(() => CborSerializer.Deserialize<BigInteger>(Convert.FromHexString("C25F4101FF"), noIndefinite));
        var twoItems = new CborSerializerOptions(readerOptions: new(maxItems: 2));
        Assert.Throws<InvalidDataException>(() => CborSerializer.Deserialize<BigInteger>(Convert.FromHexString("C25F4101FF"), twoItems));
        var output = new ArrayBufferWriter<byte>();
        var tooShort = new CborSerializerOptions(readerOptions: new(maxEncodedLength: 10));
        Assert.Throws<InvalidDataException>(() => CborSerializer.Serialize(output, BigInteger.One << 64, tooShort));
        Assert.Equal(0, output.WrittenCount);
    }

    [Fact]
    public void TagContentUsesOverridesButBignumStructuralBytesKeepTheirContract()
    {
        var overrides = CborFormatterFactory.Combine(new OffsetFactory(1), new RejectBytesFactory());
        var options = new CborSerializerOptions(CborFormatterFactory.Combine(overrides, WireValueFactory.Instance));
        Assert.Equal("C002", Convert.ToHexString(CborSerializer.Serialize(new CborTagged<int>(0, 1), options)));
        Assert.Equal(1, CborSerializer.Deserialize<CborTagged<int>>([0xc0, 2], options).Value);
        Assert.Equal(BigInteger.One << 64, CborSerializer.Deserialize<BigInteger>(CborSerializer.Serialize(BigInteger.One << 64, options), options));
    }

    [Fact]
    public void DirectStringAndCollectionReadersRejectEveryTruncatedExtendedHeader()
    {
        foreach (byte prefix in new byte[] { 0x58, 0x59, 0x5a, 0x5b, 0x78, 0x79, 0x7a, 0x7b, 0x98, 0x99, 0x9a, 0x9b, 0xb8, 0xb9, 0xba, 0xbb })
        {
            int headerLength = 1 + (1 << ((prefix & 31) - 24));
            for (int length = 1; length < headerLength; length++)
            {
                var bytes = new byte[length];
                bytes[0] = prefix;
                var reader = new CompatibleReadOnlySequenceReadBuffer(new ReadOnlySequence<byte>(bytes));
                try
                {
                    Assert.Throws<EndOfStreamException>(() =>
                    {
                        switch (prefix >> 5)
                        {
                            case 2: reader.ReadByteString(); break;
                            case 3: reader.ReadTextString(); break;
                            case 4: reader.ReadArrayHeader(); break;
                            case 5: reader.ReadMapHeader(); break;
                        }
                    });
                    Assert.Equal(0, reader.BytesConsumed);
                }
                finally
                {
                    reader.Dispose();
                }
            }
        }
    }

    [Fact]
    public void SingleNarrowingRejectsFiniteValuesJustOutsideItsRange()
    {
        double aboveMaximum = Math.BitIncrement(float.MaxValue);
        Assert.Throws<OverflowException>(() => CborSerializer.Deserialize<float>(CborSerializer.Serialize(aboveMaximum)));
        Assert.Throws<OverflowException>(() => CborSerializer.Deserialize<float>(CborSerializer.Serialize(-aboveMaximum)));
        Assert.Equal(float.MaxValue, CborSerializer.Deserialize<float>(CborSerializer.Serialize((double)float.MaxValue)));
        Assert.Equal(float.PositiveInfinity, CborSerializer.Deserialize<float>(CborSerializer.Serialize(double.PositiveInfinity)));
        Assert.True(float.IsNaN(CborSerializer.Deserialize<float>(CborSerializer.Serialize(double.NaN))));
    }

    [Fact]
    public void WarmSmallBigIntegerWriterOperationsDoNotAllocateMagnitudeArrays()
    {
        var output = new ArrayBufferWriter<byte>(64);
        BigInteger value = ulong.MaxValue;
        for (int i = 0; i < 128; i++)
        {
            output.Clear();
            CborSerializer.Serialize(output, value);
        }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1024; i++)
        {
            output.Clear();
            CborSerializer.Serialize(output, value);
        }
        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
    }

#if !CBOR_NETSTANDARD20 && !CBOR_NETSTANDARD21
    [Fact]
    public void AllHalfBitPatternsRoundTripWithCanonicalNaNsAndSignedZero()
    {
        for (int i = 0; i <= ushort.MaxValue; i++)
        {
            Half value = BitConverter.UInt16BitsToHalf((ushort)i);
            byte[] encoded = CborSerializer.Serialize(value);
            Assert.Equal(3, encoded.Length);
            Half decoded = CborSerializer.Deserialize<Half>(encoded);
            if (Half.IsNaN(value))
            {
                Assert.Equal("F97E00", Convert.ToHexString(encoded));
                Assert.True(Half.IsNaN(decoded));
            }
            else
            {
                Assert.Equal((ushort)i, BitConverter.HalfToUInt16Bits(decoded));
            }
        }
        AssertRoundTrip(new CborTagged<Half>(1000, (Half)1.5), new(HalfWireFactory.Instance));
        Assert.Throws<OverflowException>(() => CborSerializer.Deserialize<Half>(CborSerializer.Serialize(70000d)));
        Assert.Throws<OverflowException>(() => CborSerializer.Deserialize<Half>(CborSerializer.Serialize(Math.BitIncrement((double)Half.MaxValue))));
        Assert.Throws<OverflowException>(() => CborSerializer.Deserialize<Half>(CborSerializer.Serialize(Math.BitDecrement((double)Half.MinValue))));
        Assert.Equal((Half)1.1, CborSerializer.Deserialize<Half>(CborSerializer.Serialize(1.1d)));
        Assert.Throws<InvalidDataException>(() => CborSerializer.Deserialize<Half>([1]));
    }
#endif

    private static void AssertRoundTrip<T>(T value, CborSerializerOptions? options = null)
    {
        byte[] encoded = CborSerializer.Serialize(value, options);
        Assert.Equal(CborDecodeResult.Success, CborValidation.TryValidate(encoded));
        Assert.Equal(value, CborSerializer.Deserialize<T>(encoded, options));
        for (int i = 0; i <= encoded.Length; i++)
        {
            var sequence = IntegerHarness.Split(encoded, i);
            Assert.Equal(value, CborSerializer.Deserialize<T>(in sequence, options));
        }
        var fragmented = IntegerHarness.Fragment(encoded);
        Assert.Equal(value, CborSerializer.Deserialize<T>(in fragmented, options));
        for (int i = 0; i < encoded.Length; i++)
        {
            Assert.Throws<EndOfStreamException>(() => CborSerializer.Deserialize<T>(encoded.AsSpan(0, i), options));
        }
        var writer = new ArrayBufferWriter<byte>();
        CborSerializer.Serialize(writer, value, options);
        Assert.Equal(encoded, writer.WrittenSpan.ToArray());
    }

    private sealed class RejectBytesFactory : TestFactory
    {
        protected override object? Create<W, R>(Type valueType) => valueType == typeof(byte[]) ? new Formatter<W, R>() : null;
        private sealed class Formatter<W, R> : ICborFormatter<W, R, byte[]>
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
            public void Serialize(ref W buffer, ref CborSerializationContext context, byte[] value) => throw new InvalidOperationException("Unexpected byte[] override.");
            public byte[] Deserialize(ref R buffer, ref CborDeserializationContext context) => throw new InvalidOperationException("Unexpected byte[] override.");
        }
    }

}

[CborFactory(typeof(CborTagged<CborTagged<CborSimpleValue>>), typeof(CborTagged<string>), typeof(CborTagged<int>),
    typeof(CborTagged<BigInteger>), typeof(CborTagged<List<CborInteger>>))]
public partial class WireValueFactory;

#if !CBOR_NETSTANDARD20 && !CBOR_NETSTANDARD21
[CborFactory(typeof(CborTagged<Half>))]
public partial class HalfWireFactory;
#endif
