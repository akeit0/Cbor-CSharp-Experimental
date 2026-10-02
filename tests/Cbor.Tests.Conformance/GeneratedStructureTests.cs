using System.Formats.Cbor;
using Cbor.Testing;

namespace Cbor.Tests.Conformance;

public sealed class GeneratedStructureTests
{
    [Fact]
    public void GeneratedNestedValuesAndMutationsAgreeWithIndependentReader()
    {
        var random = new Random(89492026);
        for (int iteration = 0; iteration < 2000; iteration++)
        {
            var writer = new CborWriter(CborConformanceMode.Lax);
            WriteGeneratedValue(writer, random, 0);
            byte[] input = writer.Encode();
            Assert.True(OracleAccepts(input), "Generated oracle value is invalid: " + Convert.ToHexString(input));
            var source = IntegerHarness.Fragment(input);
            Assert.Equal(CborDecodeResult.Success, CborValidation.TryValidate(in source));

            input[random.Next(input.Length)] ^= (byte)(1 << random.Next(8));
            bool expected = OracleAccepts(input);
            bool actual = CborValidation.TryValidate(input) == CborDecodeResult.Success;
            Assert.True(expected == actual, "Mutated acceptance differs for " + Convert.ToHexString(input));
        }
    }

    [Fact]
    public void BoundedRandomByteInputsAgreeWithIndependentStructuralReader()
    {
        var random = new Random(20261002);
        for (int iteration = 0; iteration < 20000; iteration++)
        {
            byte[] input = new byte[random.Next(1, 65)];
            random.NextBytes(input);
            bool expected = OracleAccepts(input);
            bool actual = CborValidation.TryValidate(input) == CborDecodeResult.Success;
            Assert.True(expected == actual, "Acceptance differs for " + Convert.ToHexString(input));
        }
    }

    [Theory]
    [InlineData("f81f")]
    [InlineData("61ff")]
    public void OracleUsesRfcValidityRatherThanLaxReplacementOrReservedSimpleValues(string hex)
    {
        byte[] input = Convert.FromHexString(hex);
        Assert.False(OracleAccepts(input));
        Assert.Equal(CborDecodeResult.InvalidData, CborValidation.TryValidate(input));
    }

    [Fact]
    public void GeneratedSinglePrecisionValuesUseTheIndependentPreferredWidth()
    {
        var random = new Random(65536);
        byte[] raw = new byte[4];
        byte[] encoded = new byte[9];
        for (int iteration = 0; iteration < 10000; iteration++)
        {
            random.NextBytes(raw);
            float value = BitConverter.Int32BitsToSingle(System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(raw));
            var oracle = new CborWriter(CborConformanceMode.Canonical);
            oracle.WriteDouble(value);
            Assert.True(CborPrimitives.TryWriteDouble(encoded, value, out int written));
            Assert.Equal(oracle.Encode(), encoded.AsSpan(0, written).ToArray());
        }
    }

    private static bool OracleAccepts(byte[] input)
    {
        try
        {
            var reader = new CborReader(input, CborConformanceMode.Lax);
            ReadValue(reader);
            return reader.BytesRemaining == 0;
        }
        catch (CborContentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static void WriteGeneratedValue(CborWriter writer, Random random, int depth)
    {
        int kind = random.Next(depth == 4 ? 6 : 11);
        int count = random.Next(5);
        switch (kind)
        {
            case 0:
                writer.WriteInt64(random.NextInt64());
                break;
            case 1:
                writer.WriteCborNegativeIntegerRepresentation((ulong)random.NextInt64());
                break;
            case 2:
                writer.WriteDouble(BitConverter.Int64BitsToDouble(random.NextInt64()));
                break;
            case 3:
                byte[] bytes = new byte[random.Next(65)];
                random.NextBytes(bytes);
                writer.WriteByteString(bytes);
                break;
            case 4:
                writer.WriteTextString(count % 2 == 0 ? "水𐅑\0" : "CBOR ü");
                break;
            case 5:
                writer.WriteSimpleValue((System.Formats.Cbor.CborSimpleValue)(count < 2 ? random.Next(24) : random.Next(32, 256)));
                break;
            case 6:
                writer.WriteStartArray(random.Next(2) == 0 ? null : count);
                for (int i = 0; i < count; i++)
                {
                    WriteGeneratedValue(writer, random, depth + 1);
                }
                writer.WriteEndArray();
                break;
            case 7:
                writer.WriteStartMap(random.Next(2) == 0 ? null : count);
                for (int i = 0; i < count; i++)
                {
                    writer.WriteInt64(i);
                    WriteGeneratedValue(writer, random, depth + 1);
                }
                writer.WriteEndMap();
                break;
            case 8:
                writer.WriteTag((CborTag)123456789);
                WriteGeneratedValue(writer, random, depth + 1);
                break;
            case 9:
                writer.WriteStartIndefiniteLengthByteString();
                for (int i = 0; i < count; i++)
                {
                    writer.WriteByteString(new byte[] { 1, 2, 3 });
                }
                writer.WriteEndIndefiniteLengthByteString();
                break;
            default:
                writer.WriteStartIndefiniteLengthTextString();
                for (int i = 0; i < count; i++)
                {
                    writer.WriteTextString("水𐅑");
                }
                writer.WriteEndIndefiniteLengthTextString();
                break;
        }
    }

    // Typed reads validate UTF-8 rather than only slicing encoded bytes.
    private static void ReadValue(CborReader reader)
    {
        switch (reader.PeekState())
        {
            case CborReaderState.UnsignedInteger:
                reader.ReadUInt64();
                break;
            case CborReaderState.NegativeInteger:
                reader.ReadCborNegativeIntegerRepresentation();
                break;
            case CborReaderState.ByteString:
            case CborReaderState.StartIndefiniteLengthByteString:
                reader.ReadByteString();
                break;
            case CborReaderState.TextString:
            case CborReaderState.StartIndefiniteLengthTextString:
                // Lax mode allows duplicate map keys but also replaces invalid UTF-8.
                // Validate each text item under Strict independently, retaining Lax map semantics.
                ReadOnlyMemory<byte> textEncoding = reader.ReadEncodedValue();
                var strictTextReader = new CborReader(textEncoding, CborConformanceMode.Strict);
                strictTextReader.ReadTextString();
                break;
            case CborReaderState.StartArray:
                reader.ReadStartArray();
                while (reader.PeekState() != CborReaderState.EndArray)
                {
                    ReadValue(reader);
                }
                reader.ReadEndArray();
                break;
            case CborReaderState.StartMap:
                reader.ReadStartMap();
                while (reader.PeekState() != CborReaderState.EndMap)
                {
                    ReadValue(reader);
                    ReadValue(reader);
                }
                reader.ReadEndMap();
                break;
            case CborReaderState.Tag:
                reader.ReadTag();
                ReadValue(reader);
                break;
            case CborReaderState.HalfPrecisionFloat:
            case CborReaderState.SinglePrecisionFloat:
            case CborReaderState.DoublePrecisionFloat:
                reader.ReadDouble();
                break;
            default:
                ReadOnlyMemory<byte> simpleEncoding = reader.ReadEncodedValue();
                var strictSimpleReader = new CborReader(simpleEncoding, CborConformanceMode.Strict);
                strictSimpleReader.ReadSimpleValue();
                break;
        }
    }
}
