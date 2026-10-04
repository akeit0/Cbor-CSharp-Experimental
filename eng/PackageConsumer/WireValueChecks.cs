using System.Buffers;
using System.Numerics;
using Cbor;

namespace Cbor.Samples;

internal static class WireValueChecks
{
    internal static void Run()
    {
        var factory = CborFormatterFactory.Combine(SampleWireFactory.Instance, CborFormatterFactory.Builtin);
        var options = new CborSerializerOptions(factory);
        CheckArray<sbyte>([sbyte.MinValue, -1, 0, sbyte.MaxValue], "84387F2000187F", options);
        CheckArray<short>([short.MinValue, -1, 0, short.MaxValue], "84397FFF2000197FFF", options);
        CheckArray<ushort>([0, 23, 256, ushort.MaxValue], "84001719010019FFFF", options);
        CheckArray<int>([int.MinValue, -1, 0, int.MaxValue], "843A7FFFFFFF20001A7FFFFFFF", options);
        CheckArray<uint>([0, 23, 65536, uint.MaxValue], "8400171A000100001AFFFFFFFF", options);
        CheckArray<long>([long.MinValue, -1, 0, long.MaxValue], "843B7FFFFFFFFFFFFFFF20001B7FFFFFFFFFFFFFFF", options);
        CheckArray<ulong>([0, 23, (ulong)uint.MaxValue + 1, ulong.MaxValue], "8400171B00000001000000001BFFFFFFFFFFFFFFFF", options);
        var value = new CborTagged<CborTagged<CborSimpleValue>>(ulong.MaxValue, new(1000, CborSimpleValue.Undefined));
        byte[] encoded = CborSerializer.Serialize(value, options);
        if (CborSerializer.Deserialize<CborTagged<CborTagged<CborSimpleValue>>>(encoded, options) != value)
        {
            throw new InvalidOperationException("Installed factory and generated tag-chain contract failed.");
        }
        foreach (BigInteger number in new[] { BigInteger.One << 64, -(BigInteger.One << 64), -(BigInteger.One << 4096), (BigInteger.One << 2048) - 1 })
        {
            byte[] bytes = CborSerializer.Serialize(number, options);
            var source = new ReadOnlySequence<byte>(bytes);
            if (CborSerializer.Deserialize<BigInteger>(in source, options) != number)
            {
                throw new InvalidOperationException("Installed bignum contract failed.");
            }
        }
        var integer = CborInteger.FromNegativeArgument(ulong.MaxValue);
        if (CborSerializer.Deserialize<CborInteger>(CborSerializer.Serialize(integer, options), options) != integer ||
            BitConverter.HalfToUInt16Bits(CborSerializer.Deserialize<Half>(CborSerializer.Serialize(BitConverter.UInt16BitsToHalf(0x8000)))) != 0x8000)
        {
            throw new InvalidOperationException("Installed full-range integer or Half contract failed.");
        }
    }

    private static void CheckArray<T>(T[] pattern, string expected, CborSerializerOptions options)
    {
        if (Convert.ToHexString(CborSerializer.Serialize(pattern, options)) != expected)
        {
            throw new InvalidOperationException("Installed integer-array codec changed RFC tokens.");
        }
        const int Count = 1025;
        var values = new T[Count];
        for (int i = 0; i < values.Length; i++) { values[i] = pattern[i % pattern.Length]; }
        byte[] bytes = CborSerializer.Serialize(values, options);
        if (!CborSerializer.Deserialize<T[]>(bytes, options).SequenceEqual(values))
        {
            throw new InvalidOperationException("Installed integer-array batch contract failed.");
        }
        var sequence = new ReadOnlySequence<byte>(bytes);
        if (!CborSerializer.Deserialize<T[]>(in sequence, options).SequenceEqual(values))
        {
            throw new InvalidOperationException("Installed integer-array sequence contract failed.");
        }
    }
}

[CborFactory(typeof(CborTagged<CborTagged<CborSimpleValue>>), typeof(CborTagged<BigInteger>), typeof(CborTagged<Half>),
    typeof(sbyte[]), typeof(short[]), typeof(ushort[]), typeof(int[]), typeof(uint[]), typeof(long[]), typeof(ulong[]))]
public partial class SampleWireFactory;
