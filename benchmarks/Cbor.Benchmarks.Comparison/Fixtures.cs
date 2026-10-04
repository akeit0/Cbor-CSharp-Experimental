using System.Globalization;

namespace Cbor.Benchmarks.Comparison;

internal static class Fixtures
{
    internal const int CollectionCount = 1024;
    internal const int LinesPerOrder = 8;
    internal const int SmallOrderCount = 1;
    internal const int LargeOrderCount = 64;
    internal const string CborName = "Cbor";
    internal const string MessagePackName = "MessagePackV4";
    internal const string RedoxName = "REDox";
    internal const string SystemFormatsName = "SystemFormatsCbor";
    internal const string PeterName = "PeterOCbor";

    internal static int[] CreateArray()
    {
        var values = new int[CollectionCount];
        for (int i = 0; i < values.Length; i++)
        {
            values[i] = (i % 8) switch
            {
                0 => i, 1 => -i, 2 => int.MaxValue - i, 3 => int.MinValue + i,
                4 => 23, 5 => 24, 6 => 255, _ => 256,
            };
        }
        return values;
    }

    internal static Dictionary<string, int> CreateMap()
    {
        var values = new Dictionary<string, int>(CollectionCount, StringComparer.Ordinal);
        for (int i = 0; i < CollectionCount; i++)
        {
            values.Add("key-" + i.ToString(CultureInfo.InvariantCulture) + "-水", i % 2 == 0 ? i : -i);
        }
        return values;
    }

    internal static OrderBatch CreateBatch(int count)
    {
        var batch = new OrderBatch { BatchId = 1_000, Orders = new List<Order>(count) };
        for (int i = 0; i < count; i++)
        {
            var order = new Order
            {
                Id = i % 2 == 0 ? i : -i,
                Customer = "customer-水😀-" + i.ToString(CultureInfo.InvariantCulture),
                Lines = new List<OrderLine>(LinesPerOrder),
            };
            for (int line = 0; line < LinesPerOrder; line++)
            {
                order.Lines.Add(new OrderLine
                {
                    Sku = "sku-" + line.ToString(CultureInfo.InvariantCulture),
                    Quantity = line + 1,
                    Price = 1_000 + line,
                });
            }
            batch.Orders.Add(order);
        }
        return batch;
    }

    internal static void EqualArray(int[] expected, int[] actual)
    {
        if (!expected.AsSpan().SequenceEqual(actual)) { throw new InvalidDataException("Integer array differs."); }
    }

    internal static void EqualMap(Dictionary<string, int> expected, Dictionary<string, int> actual)
    {
        if (actual.Count != expected.Count) { throw new InvalidDataException("Map count differs."); }
        foreach (var pair in expected)
        {
            if (!actual.TryGetValue(pair.Key, out int value) || value != pair.Value) { throw new InvalidDataException("Map entry differs."); }
        }
    }

    internal static void EqualBatch(OrderBatch expected, OrderBatch actual)
    {
        if (actual.BatchId != expected.BatchId || actual.Orders.Count != expected.Orders.Count) { throw new InvalidDataException("Batch differs."); }
        for (int i = 0; i < expected.Orders.Count; i++)
        {
            var left = expected.Orders[i];
            var right = actual.Orders[i];
            if (left.Id != right.Id || left.Customer != right.Customer || left.Lines.Count != right.Lines.Count) { throw new InvalidDataException("Order differs."); }
            for (int j = 0; j < left.Lines.Count; j++)
            {
                var a = left.Lines[j];
                var b = right.Lines[j];
                if (a.Sku != b.Sku || a.Quantity != b.Quantity || a.Price != b.Price) { throw new InvalidDataException("Order line differs."); }
            }
        }
    }

    internal static void RequireEqualBytes(byte[] expected, byte[] actual)
    {
        if (!expected.AsSpan().SequenceEqual(actual)) { throw new InvalidDataException("Cbor and System.Formats.Cbor fixture wire bytes differ."); }
    }

    internal static void PrintSizes(string workload, Dictionary<string, byte[]> payloads)
    {
        foreach (var pair in payloads) { Console.WriteLine($"Payload: {workload} {pair.Key} {pair.Value.Length} bytes"); }
    }
}
