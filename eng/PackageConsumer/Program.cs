using System.Buffers;
using Cbor;
using SerializerFoundation;

var output = new ArrayBufferWriter<byte>();
#if NET9_0_OR_GREATER
var writer = new BufferWriterWriteBuffer(output);
#else
var writer = new CompatibleBufferWriterWriteBuffer(output);
#endif
try
{
    writer.WriteMapHeader(2);
    writer.WriteTextString("name");
    writer.WriteTextString("CBOR 水");
    writer.WriteTextString("value");
    writer.WriteDouble(-0.0);
    writer.Flush();
}
finally
{
    writer.Dispose();
}

byte[] encoded = output.WrittenSpan.ToArray();
if (CborValidation.TryValidate(encoded) != CborDecodeResult.Success)
{
    throw new InvalidOperationException("The packaged runtime rejected the compound fixture.");
}

#if NET9_0_OR_GREATER
var reader = new ReadOnlySpanReadBuffer(encoded);
#else
var reader = new CompatibleReadOnlySequenceReadBuffer(new ReadOnlySequence<byte>(encoded));
#endif
try
{
    if (reader.ReadMapHeader() != 2 || reader.ReadTextString() != "name" ||
        reader.ReadTextString() != "CBOR 水" || reader.ReadTextString() != "value" ||
        BitConverter.DoubleToInt64Bits(reader.ReadDouble()) != long.MinValue || reader.BytesRemaining != 0)
    {
        throw new InvalidOperationException("The packaged runtime did not preserve the fixture.");
    }
}
finally
{
    reader.Dispose();
}

var typedOptions = new CborSerializerOptions(Cbor.Samples.SampleResolver.Instance);
if (CborSerializer.Deserialize<string>(Convert.FromHexString("7F61686169FF")) != "hi" ||
    !CborSerializer.Deserialize<byte[]>(Convert.FromHexString("5F4201024103FF")).SequenceEqual(new byte[] { 1, 2, 3 }))
{
    throw new InvalidOperationException("Installed package struct materializer specialization failed.");
}

var envelope = new Cbor.Samples.SampleEnvelope
{
    Id = 42,
    Name = "水😀",
    Values = [new Cbor.Samples.SampleValue(long.MinValue, "immutable"), new Cbor.Samples.SampleValue(1000, null)],
};
byte[] typedEncoded = CborSerializer.Serialize(envelope, typedOptions);
var typed = CborSerializer.Deserialize<Cbor.Samples.SampleEnvelope>(typedEncoded, typedOptions);
if (typed.Id != 42 || typed.Name != "水😀" || typed.Values is not { Count: 2 } ||
    typed.Values[0].Count != long.MinValue || typed.Values[1].Label is not null)
{
    throw new InvalidOperationException("Generated object serialization failed.");
}

GenericModelChecks.Run(typedOptions);
Console.WriteLine("Installed CBOR package consumer passed: primitives and generated mutable/immutable, generic, and inherited models.");
