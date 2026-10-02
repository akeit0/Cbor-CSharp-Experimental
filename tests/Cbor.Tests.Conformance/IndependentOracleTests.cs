using Cbor.Testing;
using System.Formats.Cbor;

namespace Cbor.Tests.Conformance;

public sealed class IndependentOracleTests
{
    public static TheoryData<string> Valid => new(RfcVectors.Valid);

    [Theory]
    [MemberData(nameof(Valid))]
    public void RfcStructuralAcceptanceAgreesWithBcl(string hex)
    {
        byte[] input = Convert.FromHexString(hex);
        var oracle = new CborReader(input, CborConformanceMode.Lax);
        oracle.SkipValue();
        Assert.Equal(0, oracle.BytesRemaining);
        Assert.Equal(CborDecodeResult.Success, CborValidation.TryValidate(input));
    }

    [Fact]
    public void GeneratedIntegerAndFloatValuesAgreeWithIndependentWriter()
    {
        var random = new Random(8949);
        byte[] raw = new byte[8];
        byte[] encoded = new byte[9];
        for (int iteration = 0; iteration < 10000; iteration++)
        {
            random.NextBytes(raw);
            long signed = System.Buffers.Binary.BinaryPrimitives.ReadInt64BigEndian(raw);
            var integerOracle = new CborWriter();
            integerOracle.WriteInt64(signed);
            Assert.True(CborPrimitives.TryWriteInt64(encoded, signed, out int written));
            Assert.Equal(integerOracle.Encode(), encoded.AsSpan(0, written).ToArray());

            double value = BitConverter.Int64BitsToDouble(signed);
            var floatOracle = new CborWriter(CborConformanceMode.Canonical);
            floatOracle.WriteDouble(value);
            Assert.True(CborPrimitives.TryWriteDouble(encoded, value, out written));
            Assert.Equal(floatOracle.Encode(), encoded.AsSpan(0, written).ToArray());
            Assert.Equal(CborDecodeResult.Success, CborValidation.TryValidate(encoded.AsSpan(0, written),
                new CborReaderOptions(requirePreferredEncoding: true)));
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("IETF")]
    [InlineData("\"\\")]
    [InlineData("ü")]
    [InlineData("水")]
    [InlineData("𐅑")]
    public void Utf8WritersAndSegmentedReadersAgreeWithBcl(string value)
    {
        var output = new System.Buffers.ArrayBufferWriter<byte>();
        var writer = new SerializerFoundation.BufferWriterWriteBuffer(output);
        try
        {
            writer.WriteTextString(value);
            writer.Flush();
        }
        finally
        {
            writer.Dispose();
        }

        var oracle = new CborWriter();
        oracle.WriteTextString(value);
        Assert.Equal(oracle.Encode(), output.WrittenSpan.ToArray());

        byte[] encoded = output.WrittenSpan.ToArray();
        for (int offset = 0; offset <= encoded.Length; offset++)
        {
            var source = IntegerHarness.Split(encoded, offset);
            var reader = new SerializerFoundation.ReadOnlySequenceReadBuffer(in source);
            try
            {
                Assert.Equal(value, reader.ReadTextString());
                Assert.Equal(0, reader.BytesRemaining);
            }
            finally
            {
                reader.Dispose();
            }
        }
    }
}
