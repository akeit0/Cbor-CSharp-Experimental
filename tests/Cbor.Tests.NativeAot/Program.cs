using Cbor.Testing;
using Cbor;

Cbor.Samples.WireValueChecks.Run();

ulong[] values = [0, 23, 24, 255, 256, 65535, 65536, uint.MaxValue, (ulong)uint.MaxValue + 1, ulong.MaxValue];
string[] textValues = [new string('a', 256), string.Concat(Enumerable.Repeat("水😀", 64))];
foreach (string text in textValues)
{
    byte[] encoded = CborSerializer.Serialize(text);
    if (CborValidation.TryValidate(encoded) != CborDecodeResult.Success)
    {
        throw new InvalidOperationException("Native AOT bulk UTF-8 validation failed.");
    }

    for (int offset = 0; offset <= encoded.Length; offset++)
    {
        var sequence = IntegerHarness.Split(encoded, offset);
        if (CborValidation.TryValidate(in sequence) != CborDecodeResult.Success)
        {
            throw new InvalidOperationException("Native AOT UTF-8 seam validation failed.");
        }
    }

    var fragmented = IntegerHarness.Fragment(encoded);
    if (CborValidation.TryValidate(in fragmented) != CborDecodeResult.Success)
    {
        throw new InvalidOperationException("Native AOT fragmented UTF-8 validation failed.");
    }
}

if (CborValidation.TryValidate(Convert.FromHexString("647FEDA080")) != CborDecodeResult.InvalidData)
{
    throw new InvalidOperationException("Native AOT bulk UTF-8 surrogate rejection failed.");
}

double[] floatingValues = [0.0, -0.0, 1.5, 100000.0, 1.1, double.NaN, double.PositiveInfinity, double.MinValue];
foreach (double value in floatingValues)
{
    byte[] encoded = CborSerializer.Serialize(value);
    double decoded = CborSerializer.Deserialize<double>(encoded);
    if (double.IsNaN(value))
    {
        if (Convert.ToHexString(encoded) != "F97E00" || !double.IsNaN(decoded))
        {
            throw new InvalidOperationException("Native AOT preferred NaN encoding failed.");
        }
    }
    else if (BitConverter.DoubleToInt64Bits(value) != BitConverter.DoubleToInt64Bits(decoded))
    {
        throw new InvalidOperationException("Native AOT preferred float reference stores failed.");
    }
}

foreach (ulong value in values)
{
    byte[] encoded = IntegerHarness.Encode(value);
    if (IntegerHarness.Decode(encoded) != value)
    {
        throw new InvalidOperationException("Native AOT unsigned integer round trip failed.");
    }

    for (int offset = 0; offset <= encoded.Length; offset++)
    {
        var source = IntegerHarness.Split(encoded, offset);
        if (IntegerHarness.Decode(in source) != value)
        {
            throw new InvalidOperationException("Native AOT segmented input failed.");
        }
    }
}

if (Convert.ToHexString(IntegerHarness.Encode(1000)) != "1903E8")
{
    throw new InvalidOperationException("Native AOT RFC vector failed.");
}

if (RfcVectors.Valid.Length != 81)
{
    throw new InvalidOperationException("The Native AOT executable lost RFC fixtures.");
}

foreach (string hex in RfcVectors.Valid)
{
    byte[] encoded = Convert.FromHexString(hex);
    if (Cbor.CborValidation.TryValidate(encoded) != Cbor.CborDecodeResult.Success)
    {
        throw new InvalidOperationException("Native AOT RFC validation failed: " + hex);
    }

    for (int offset = 0; offset <= encoded.Length; offset++)
    {
        var source = IntegerHarness.Split(encoded, offset);
        if (Cbor.CborValidation.TryValidate(in source) != Cbor.CborDecodeResult.Success)
        {
            throw new InvalidOperationException("Native AOT segmented RFC validation failed: " + hex);
        }
    }
}

foreach (string hex in RfcVectors.Invalid)
{
    if (Cbor.CborValidation.TryValidate(Convert.FromHexString(hex)) == Cbor.CborDecodeResult.Success)
    {
        throw new InvalidOperationException("Native AOT malformed RFC vector was accepted: " + hex);
    }
}

byte[] deep = Enumerable.Repeat((byte)0x81, 1024).Append((byte)0x00).ToArray();
if (Cbor.CborValidation.TryValidate(deep, new Cbor.CborReaderOptions(maxDepth: 1024)) != Cbor.CborDecodeResult.Success)
{
    throw new InvalidOperationException("Native AOT iterative deep validation failed.");
}

var typedOptions = new CborSerializerOptions(Cbor.Samples.SampleFactory.Instance);
if (CborSerializer.Deserialize<string>(Convert.FromHexString("7F61686169FF")) != "hi" ||
    !CborSerializer.Deserialize<byte[]>(Convert.FromHexString("5F4201024103FF")).SequenceEqual(new byte[] { 1, 2, 3 }))
{
    throw new InvalidOperationException("Native AOT struct materializer specialization failed.");
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

for (int offset = 0; offset <= typedEncoded.Length; offset++)
{
    var source = IntegerHarness.Split(typedEncoded, offset);
    var decoded = CborSerializer.Deserialize<Cbor.Samples.SampleEnvelope>(in source, typedOptions);
    if (decoded.Values![0].Count != long.MinValue)
    {
        throw new InvalidOperationException("Native AOT generated segmented object serialization failed.");
    }
}

var dictionary = new Dictionary<long, string> { [long.MinValue] = "min", [long.MaxValue] = "max" };
var restoredDictionary = CborSerializer.Deserialize<Dictionary<long, string>>(CborSerializer.Serialize(dictionary, typedOptions), typedOptions);
if (restoredDictionary[long.MinValue] != "min")
{
    throw new InvalidOperationException("Native AOT process-keyed dictionary serialization failed.");
}

GenericModelChecks.Run(typedOptions);
Console.WriteLine("CBOR Native AOT passed: bulk/seamed UTF-8, preferred float reference stores, RFC corpus, segmented primitives/generated models, generic/inherited constructors, keyed dictionaries, and deep traversal.");
