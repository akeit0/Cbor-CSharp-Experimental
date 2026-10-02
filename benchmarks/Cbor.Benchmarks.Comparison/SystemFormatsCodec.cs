using System.Formats.Cbor;

namespace Cbor.Benchmarks.Comparison;

// System.Formats.Cbor supplies tokens, not a CLR model serializer. These are schema-specific adapters.
internal static class SystemFormatsCodec
{
    private const int BatchFieldCount = 2;
    private const int OrderFieldCount = 3;
    private const int LineFieldCount = 3;
    private const int IdKey = 0;
    private const int SecondKey = 1;
    private const int ThirdKey = 2;

    internal static byte[] Encode(int[] value)
    {
        var writer = new CborWriter(CborConformanceMode.Strict);
        writer.WriteStartArray(value.Length);
        foreach (int item in value) { writer.WriteInt32(item); }
        writer.WriteEndArray();
        return writer.Encode();
    }

    internal static int[] DecodeArray(byte[] data)
    {
        var reader = new CborReader(data, CborConformanceMode.Strict);
        int? count = reader.ReadStartArray();
        int[] result;
        if (count is { } length)
        {
            result = new int[length];
            for (int i = 0; i < length; i++) { result[i] = reader.ReadInt32(); }
        }
        else
        {
            var items = new List<int>();
            while (reader.PeekState() != CborReaderState.EndArray) { items.Add(reader.ReadInt32()); }
            result = items.ToArray();
        }
        reader.ReadEndArray();
        RequireEnd(reader);
        return result;
    }

    internal static byte[] Encode(Dictionary<string, int> value)
    {
        var writer = new CborWriter(CborConformanceMode.Strict);
        writer.WriteStartMap(value.Count);
        foreach (var item in value) { writer.WriteTextString(item.Key); writer.WriteInt32(item.Value); }
        writer.WriteEndMap();
        return writer.Encode();
    }

    internal static Dictionary<string, int> DecodeMap(byte[] data)
    {
        var reader = new CborReader(data, CborConformanceMode.Strict);
        int? count = reader.ReadStartMap();
        var result = new Dictionary<string, int>(count ?? 0, StringComparer.Ordinal);
        while (reader.PeekState() != CborReaderState.EndMap) { result.Add(reader.ReadTextString(), reader.ReadInt32()); }
        reader.ReadEndMap();
        RequireEnd(reader);
        return result;
    }

    internal static byte[] Encode(OrderBatch batch)
    {
        var writer = new CborWriter(CborConformanceMode.Strict);
        writer.WriteStartMap(BatchFieldCount);
        writer.WriteInt32(IdKey); writer.WriteInt64(batch.BatchId);
        writer.WriteInt32(SecondKey); writer.WriteStartArray(batch.Orders.Count);
        foreach (var order in batch.Orders)
        {
            writer.WriteStartMap(OrderFieldCount);
            writer.WriteInt32(IdKey); writer.WriteInt64(order.Id);
            writer.WriteInt32(SecondKey); writer.WriteTextString(order.Customer);
            writer.WriteInt32(ThirdKey); writer.WriteStartArray(order.Lines.Count);
            foreach (var line in order.Lines)
            {
                writer.WriteStartMap(LineFieldCount);
                writer.WriteInt32(IdKey); writer.WriteTextString(line.Sku);
                writer.WriteInt32(SecondKey); writer.WriteInt32(line.Quantity);
                writer.WriteInt32(ThirdKey); writer.WriteInt32(line.Price);
                writer.WriteEndMap();
            }
            writer.WriteEndArray(); writer.WriteEndMap();
        }
        writer.WriteEndArray(); writer.WriteEndMap();
        return writer.Encode();
    }

    internal static OrderBatch DecodeBatch(byte[] data)
    {
        var reader = new CborReader(data, CborConformanceMode.Strict);
        reader.ReadStartMap();
        var batch = new OrderBatch();
        int seen = 0;
        while (reader.PeekState() != CborReaderState.EndMap)
        {
            int key = ReadKey(reader, nameof(OrderBatch.BatchId), nameof(OrderBatch.Orders));
            TrackKey(ref seen, key, BatchFieldCount);
            switch (key)
            {
                case IdKey: batch.BatchId = reader.ReadInt64(); break;
                case SecondKey:
                    int? count = reader.ReadStartArray();
                    batch.Orders = new List<Order>(count ?? 0);
                    while (reader.PeekState() != CborReaderState.EndArray) { batch.Orders.Add(ReadOrder(reader)); }
                    reader.ReadEndArray();
                    break;
                default: reader.SkipValue(); break;
            }
        }
        reader.ReadEndMap();
        RequireEnd(reader);
        return batch;
    }

    private static Order ReadOrder(CborReader reader)
    {
        reader.ReadStartMap();
        var order = new Order();
        int seen = 0;
        while (reader.PeekState() != CborReaderState.EndMap)
        {
            int key = ReadKey(reader, nameof(Order.Id), nameof(Order.Customer), nameof(Order.Lines));
            TrackKey(ref seen, key, OrderFieldCount);
            switch (key)
            {
                case IdKey: order.Id = reader.ReadInt64(); break;
                case SecondKey: order.Customer = reader.ReadTextString(); break;
                case ThirdKey:
                    int? count = reader.ReadStartArray();
                    order.Lines = new List<OrderLine>(count ?? 0);
                    while (reader.PeekState() != CborReaderState.EndArray) { order.Lines.Add(ReadLine(reader)); }
                    reader.ReadEndArray();
                    break;
                default: reader.SkipValue(); break;
            }
        }
        reader.ReadEndMap();
        return order;
    }

    private static OrderLine ReadLine(CborReader reader)
    {
        reader.ReadStartMap();
        var line = new OrderLine();
        int seen = 0;
        while (reader.PeekState() != CborReaderState.EndMap)
        {
            int key = ReadKey(reader, nameof(OrderLine.Sku), nameof(OrderLine.Quantity), nameof(OrderLine.Price));
            TrackKey(ref seen, key, LineFieldCount);
            switch (key)
            {
                case IdKey: line.Sku = reader.ReadTextString(); break;
                case SecondKey: line.Quantity = reader.ReadInt32(); break;
                case ThirdKey: line.Price = reader.ReadInt32(); break;
                default: reader.SkipValue(); break;
            }
        }
        reader.ReadEndMap();
        return line;
    }

    // Also accepts the named-map contracts emitted by REDox and PeterO for independent fixture validation.
    private static int ReadKey(CborReader reader, string first, string second, string? third = null)
    {
        if (reader.PeekState() == CborReaderState.UnsignedInteger) { return reader.ReadInt32(); }
        string name = reader.ReadTextString();
        if (name.Equals(first, StringComparison.OrdinalIgnoreCase)) { return IdKey; }
        if (name.Equals(second, StringComparison.OrdinalIgnoreCase)) { return SecondKey; }
        if (third is not null && name.Equals(third, StringComparison.OrdinalIgnoreCase)) { return ThirdKey; }
        return -1;
    }

    private static void TrackKey(ref int seen, int key, int fieldCount)
    {
        if ((uint)key >= (uint)fieldCount) { return; }
        int mask = 1 << key;
        if ((seen & mask) != 0) { throw new InvalidDataException("Duplicate known model key."); }
        seen |= mask;
    }

    private static void RequireEnd(CborReader reader)
    {
        if (reader.BytesRemaining != 0) { throw new InvalidDataException("Trailing CBOR bytes."); }
    }
}
