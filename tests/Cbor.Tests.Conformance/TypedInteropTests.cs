using System.Formats.Cbor;
using Cbor.Samples;
using Cbor.Testing;

namespace Cbor.Tests.Conformance;

public sealed class TypedInteropTests
{
    private static readonly CborSerializerOptions Options = new(SampleFactory.Instance);

    [Fact]
    public void GeneratedObjectsInteroperateWithAnIndependentReaderAndWriter()
    {
        var random = new Random(94127);
        for (int i = 0; i < 1000; i++)
        {
            var value = new SampleEnvelope
            {
                Id = random.Next(),
                Name = i % 3 == 0 ? null : "水😀" + i,
                Values = Enumerable.Range(0, i % 9).Select(n => new SampleValue(
                    ((long)random.Next() << 32) | (uint)random.Next(), n % 2 == 0 ? "line" + n : null)).ToList(),
            };
            var independent = new CborWriter(CborConformanceMode.Canonical);
            WriteEnvelope(independent, value);
            byte[] expected = independent.Encode();
            Assert.Equal(expected, CborSerializer.Serialize(value, Options));

            var source = IntegerHarness.Fragment(expected);
            var decoded = CborSerializer.Deserialize<SampleEnvelope>(in source, Options);
            Assert.Equal(value.Id, decoded.Id);
            Assert.Equal(value.Name, decoded.Name);
            Assert.Equal(value.Values.Select(static x => x.Count), decoded.Values!.Select(static x => x.Count));
            Assert.Equal(value.Values.Select(static x => x.Label), decoded.Values!.Select(static x => x.Label));

            var reader = new CborReader(CborSerializer.Serialize(value, Options), CborConformanceMode.Canonical);
            Assert.Equal(3, reader.ReadStartMap());
            Assert.Equal(0, reader.ReadInt32());
            Assert.Equal(value.Id, reader.ReadInt32());
            Assert.Equal(1, reader.ReadInt32());
            Assert.Equal(value.Name, ReadNullableText(reader));
            Assert.Equal(2, reader.ReadInt32());
            Assert.Equal(value.Values.Count, reader.ReadStartArray());
            foreach (var line in value.Values)
            {
                Assert.Equal(2, reader.ReadStartMap());
                Assert.Equal(0, reader.ReadInt32());
                Assert.Equal(line.Count, reader.ReadInt64());
                Assert.Equal(1, reader.ReadInt32());
                Assert.Equal(line.Label, ReadNullableText(reader));
                reader.ReadEndMap();
            }

            reader.ReadEndArray();
            reader.ReadEndMap();
            Assert.Equal(0, reader.BytesRemaining);
        }
    }

    private static void WriteEnvelope(CborWriter writer, SampleEnvelope value)
    {
        writer.WriteStartMap(3);
        writer.WriteInt32(0);
        writer.WriteInt32(value.Id);
        writer.WriteInt32(1);
        WriteNullableText(writer, value.Name);
        writer.WriteInt32(2);
        writer.WriteStartArray(value.Values!.Count);
        foreach (var line in value.Values)
        {
            writer.WriteStartMap(2);
            writer.WriteInt32(0);
            writer.WriteInt64(line.Count);
            writer.WriteInt32(1);
            WriteNullableText(writer, line.Label);
            writer.WriteEndMap();
        }

        writer.WriteEndArray();
        writer.WriteEndMap();
    }

    private static void WriteNullableText(CborWriter writer, string? value)
    {
        if (value is null)
        {
            writer.WriteNull();
        }
        else
        {
            writer.WriteTextString(value);
        }
    }

    private static string? ReadNullableText(CborReader reader)
    {
        if (reader.PeekState() == CborReaderState.Null)
        {
            reader.ReadNull();
            return null;
        }

        return reader.ReadTextString();
    }
}
