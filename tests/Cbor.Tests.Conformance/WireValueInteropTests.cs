using System.Formats.Cbor;
using System.Numerics;
using Cbor.Testing;

namespace Cbor.Tests.Conformance;

public sealed class WireValueInteropTests
{
    [Fact]
    public void IntegerAndBignumMappingsMatchIndependentEncodingAcrossMagnitudeSizes()
    {
        var random = new Random(143089);
        for (int length = 0; length <= 512; length++)
        {
            var bytes = new byte[length];
            random.NextBytes(bytes);
            var magnitude = new BigInteger(bytes, isUnsigned: true, isBigEndian: true);
            foreach (BigInteger value in new[] { magnitude, ~magnitude })
            {
                var writer = new CborWriter(CborConformanceMode.Canonical);
                if (magnitude <= ulong.MaxValue)
                {
                    if (value.Sign < 0) { writer.WriteCborNegativeIntegerRepresentation((ulong)magnitude); }
                    else { writer.WriteUInt64((ulong)magnitude); }
                }
                else { writer.WriteBigInteger(value); }
                byte[] expected = writer.Encode();
                Assert.Equal(expected, CborSerializer.Serialize(value));
                var fragmented = IntegerHarness.Fragment(expected);
                Assert.Equal(value, CborSerializer.Deserialize<BigInteger>(in fragmented));
                var reader = new CborReader(expected, CborConformanceMode.Canonical);
                BigInteger actual = reader.PeekState() switch
                {
                    CborReaderState.UnsignedInteger => new(reader.ReadUInt64()),
                    CborReaderState.NegativeInteger => ~new BigInteger(reader.ReadCborNegativeIntegerRepresentation()),
                    _ => reader.ReadBigInteger(),
                };
                Assert.Equal(value, actual);
                Assert.Equal(0, reader.BytesRemaining);
            }
        }
    }

    [Fact]
    public void IndependentSimpleValuesIncludeNullUndefinedAndUnassignedValues()
    {
        for (int value = 0; value <= byte.MaxValue; value++)
        {
            if (value is >= 24 and <= 31) { continue; }
            var writer = new CborWriter();
            writer.WriteSimpleValue((System.Formats.Cbor.CborSimpleValue)value);
            byte[] expected = writer.Encode();
            Assert.Equal(expected, CborSerializer.Serialize(new global::Cbor.CborSimpleValue((byte)value)));
            Assert.Equal((byte)value, CborSerializer.Deserialize<global::Cbor.CborSimpleValue>(expected).Value);
        }
    }
}
