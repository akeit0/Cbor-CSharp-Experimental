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
}

[CborFactory(typeof(CborTagged<CborTagged<CborSimpleValue>>), typeof(CborTagged<BigInteger>), typeof(CborTagged<Half>))]
public partial class SampleWireFactory;
