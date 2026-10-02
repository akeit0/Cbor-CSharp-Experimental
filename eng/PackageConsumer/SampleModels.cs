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

[CborObject]
public sealed class SampleBox<T>
{
    [CborConstructor]
    public SampleBox(T value) => Value = value;

    [CborKey(0, Required = true)]
    public T Value { get; }
}

[CborObject]
public abstract class SampleBase<T>
{
    protected SampleBase(T id) => Id = id;

    [CborKey(0, Required = true)]
    public T Id { get; }

    [CborKey(1)]
    public virtual string? Label { get; set; }
}

[CborObject]
public sealed class SampleDerived : SampleBase<long>
{
    private string? label;

    [CborConstructor]
    public SampleDerived(long id, string? label) : base(id) => Label = label;

    // Inherit key 1 from the overridden virtual slot.
    public override string? Label
    {
        get => label;
        set { Assignments++; label = value?.ToUpperInvariant(); }
    }

    [CborIgnore]
    public int Assignments { get; private set; }

    [CborKey(2)]
    public int Quantity { get; set; }
}

[CborObject]
public sealed class SampleTree<T>
{
    [CborKey(0)]
    public T? Value { get; set; }

    [CborKey(1)]
    public List<SampleTree<T>>? Children { get; set; }
}

[CborFactory(typeof(SampleEnvelope), typeof(Dictionary<long, string>), typeof(SampleBox<int>),
    typeof(SampleBox<string>), typeof(SampleBox<SampleDerived>), typeof(SampleTree<int>))]
public partial class SampleFactory;
