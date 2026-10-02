using BenchmarkDotNet.Attributes;
using Cbor;

namespace Cbor.Benchmarks;

[MemoryDiagnoser]
public class NestedModelBenchmarks
{
    private static readonly CborSerializerOptions Options = new(NestedBenchmarkResolver.Instance);
    private OrderBatch batch = null!;
    private byte[] encoded = [];

    [Params(1, 64)]
    public int OrderCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        RuntimeAssetCheck.Verify();
        batch = new OrderBatch
        {
            BatchId = 1000,
            Orders = Enumerable.Range(0, OrderCount).Select(static order => new BenchOrder
            {
                Id = order,
                Customer = "customer-" + order.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Lines = Enumerable.Range(0, 8).Select(static line => new BenchLine
                {
                    Sku = "sku-" + line.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    Quantity = line + 1,
                    Price = 1000 + line,
                }).ToList(),
            }).ToList(),
        };
        encoded = CborSerializer.Serialize(batch, Options);
        Console.WriteLine(FormattableString.Invariant($"Nested payload: {encoded.Length} bytes ({OrderCount} orders)."));
        var decoded = CborSerializer.Deserialize<OrderBatch>(encoded, Options);
        if (decoded.Orders!.Count != OrderCount || decoded.Orders[0].Lines![7].Quantity != 8)
        {
            throw new InvalidOperationException("Nested benchmark fixture failed.");
        }
    }

    [Benchmark] public byte[] EncodeOrders() => CborSerializer.Serialize(batch, Options);
    [Benchmark] public OrderBatch DecodeOrders() => CborSerializer.Deserialize<OrderBatch>(encoded, Options);
}

[CborObject]
public sealed class OrderBatch
{
    [CborKey(0)] public long BatchId { get; set; }
    [CborKey(1)] public List<BenchOrder>? Orders { get; set; }
}

[CborObject]
public sealed class BenchOrder
{
    [CborKey(0)] public long Id { get; set; }
    [CborKey(1)] public string? Customer { get; set; }
    [CborKey(2)] public List<BenchLine>? Lines { get; set; }
}

[CborObject]
public sealed class BenchLine
{
    [CborKey(0)] public string? Sku { get; set; }
    [CborKey(1)] public int Quantity { get; set; }
    [CborKey(2)] public int Price { get; set; }
}

[CborResolver(typeof(OrderBatch))]
public partial class NestedBenchmarkResolver;
