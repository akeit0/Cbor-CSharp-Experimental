using MessagePack;

namespace Cbor.Benchmarks.Comparison;

[CborObject, MessagePackObject]
public sealed class OrderBatch
{
    [CborKey(0), Key(0)]
    public long BatchId { get; set; }
    [CborKey(1), Key(1)]
    public List<Order> Orders { get; set; } = [];
}

[CborObject, MessagePackObject]
public sealed class Order
{
    [CborKey(0), Key(0)]
    public long Id { get; set; }
    [CborKey(1), Key(1)]
    public string Customer { get; set; } = string.Empty;
    [CborKey(2), Key(2)]
    public List<OrderLine> Lines { get; set; } = [];
}

[CborObject, MessagePackObject]
public sealed class OrderLine
{
    [CborKey(0), Key(0)]
    public string Sku { get; set; } = string.Empty;
    [CborKey(1), Key(1)]
    public int Quantity { get; set; }
    [CborKey(2), Key(2)]
    public int Price { get; set; }
}

// Explicitly harvest the standalone collection roots for MessagePack's generated AOT factory.
[MessagePackObject]
internal sealed class CollectionRoots
{
    [Key(0)] public int[] Values { get; set; } = [];
    [Key(1)] public Dictionary<string, int> Map { get; set; } = [];
}

[CborFactory(typeof(OrderBatch), typeof(int[]), typeof(Dictionary<string, int>))]
internal sealed partial class ComparisonFactory;
