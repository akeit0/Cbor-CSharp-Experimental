using Cbor;
using Cbor.Samples;

var options = new CborSerializerOptions(SampleResolver.Instance);
var message = new SampleEnvelope
{
    Id = 42,
    Name = "CBOR 水",
    Values = [new SampleValue(-1000, "signed"), new SampleValue(1000, "unsigned range")],
};
byte[] encoded = CborSerializer.Serialize(message, options);
var restored = CborSerializer.Deserialize<SampleEnvelope>(encoded, options);
Console.WriteLine(Convert.ToHexString(encoded));
Console.WriteLine($"Id: {restored.Id}, Name: {restored.Name}");
foreach (var value in restored.Values!)
{
    Console.WriteLine($"{value.Label}: {value.Count}");
}
