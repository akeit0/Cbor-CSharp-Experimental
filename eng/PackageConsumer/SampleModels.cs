using Cbor;

namespace Cbor.Samples;

[CborObject]
public sealed class SampleEnvelope
{
    [CborKey(0, Required = true)]
    public int Id { get; set; }

    [CborKey(1)]
    public string? Name { get; set; }

    [CborKey(2)]
    public List<SampleValue>? Values { get; set; }
}

[CborObject]
public sealed class SampleValue
{
    [CborConstructor]
    public SampleValue(long count, string? label)
    {
        Count = count;
        Label = label;
    }

    [CborKey(0, Required = true)]
    public long Count { get; }

    [CborKey(1)]
    public string? Label { get; }
}

[CborResolver(typeof(SampleEnvelope), typeof(Dictionary<long, string>))]
public partial class SampleResolver;
