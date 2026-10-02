using System.Formats.Cbor;
using Cbor.Samples;
using Cbor.Testing;

namespace Cbor.Tests.Conformance;

public sealed class GenericInheritanceInteropTests
{
    private static readonly CborSerializerOptions Options = new(SampleResolver.Instance);

    [Fact]
    public void FlattenedGenericHierarchyMatchesAnIndependentCanonicalWriter()
    {
        var random = new Random(62_739);
        for (int i = 0; i < 512; i++)
        {
            long id = ((long)random.Next() << 32) | (uint)random.Next();
            if (i % 2 == 0) { id = -id; }
            string? label = i % 3 == 0 ? null : "水😀" + i;
            int quantity = random.Next();
            var model = new SampleBox<SampleDerived>(new SampleDerived(id, label) { Quantity = quantity });
            var writer = new CborWriter(CborConformanceMode.Canonical);
            writer.WriteStartMap(1);
            writer.WriteInt32(0);
            writer.WriteStartMap(3);
            writer.WriteInt32(0);
            writer.WriteInt64(id);
            writer.WriteInt32(1);
            if (label is null) { writer.WriteNull(); }
            else { writer.WriteTextString(label); }
            writer.WriteInt32(2);
            writer.WriteInt32(quantity);
            writer.WriteEndMap();
            writer.WriteEndMap();
            byte[] expected = writer.Encode();
            Assert.Equal(expected, CborSerializer.Serialize(model, Options));

            var sequence = IntegerHarness.Fragment(expected);
            var decoded = CborSerializer.Deserialize<SampleBox<SampleDerived>>(in sequence, Options).Value;
            Assert.Equal(id, decoded.Id);
            Assert.Equal(label, decoded.Label);
            Assert.Equal(quantity, decoded.Quantity);
            Assert.Equal(1, decoded.Assignments);

            var reader = new CborReader(CborSerializer.Serialize(model, Options), CborConformanceMode.Canonical);
            Assert.Equal(1, reader.ReadStartMap());
            Assert.Equal(0, reader.ReadInt32());
            Assert.Equal(3, reader.ReadStartMap());
            Assert.Equal(0, reader.ReadInt32());
            Assert.Equal(id, reader.ReadInt64());
            Assert.Equal(1, reader.ReadInt32());
            if (label is null) { reader.ReadNull(); }
            else { Assert.Equal(label, reader.ReadTextString()); }
            Assert.Equal(2, reader.ReadInt32());
            Assert.Equal(quantity, reader.ReadInt32());
            reader.ReadEndMap();
            reader.ReadEndMap();
            Assert.Equal(0, reader.BytesRemaining);
        }
    }

    [Fact]
    public void IndependentIndefiniteUnorderedMapsPreserveInheritedSlotsAndSkipUnknowns()
    {
        var writer = new CborWriter(CborConformanceMode.Lax);
        writer.WriteStartMap(null);
        writer.WriteInt32(2);
        writer.WriteInt32(7);
        writer.WriteInt32(9);
        writer.WriteStartArray(2);
        writer.WriteInt32(1000);
        writer.WriteTextString("unknown");
        writer.WriteEndArray();
        writer.WriteInt32(1);
        writer.WriteTextString("ada");
        writer.WriteInt32(0);
        writer.WriteInt64(long.MinValue);
        writer.WriteEndMap();
        var sequence = IntegerHarness.Fragment(writer.Encode());
        var decoded = CborSerializer.Deserialize<SampleDerived>(in sequence, Options);
        Assert.Equal(long.MinValue, decoded.Id);
        Assert.Equal("ADA", decoded.Label);
        Assert.Equal(7, decoded.Quantity);
        Assert.Equal(1, decoded.Assignments);
    }
}
