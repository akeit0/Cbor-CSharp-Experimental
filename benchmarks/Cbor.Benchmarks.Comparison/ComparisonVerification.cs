using System.Globalization;
using static Cbor.Benchmarks.Comparison.Fixtures;

namespace Cbor.Benchmarks.Comparison;

internal static class ComparisonVerification
{
    internal static void Run()
    {
        Codecs.VerifyAssets();
        VerifyArray(CreateArray());
        VerifyMap(CreateMap());
        foreach (int count in new[] { SmallOrderCount, LargeOrderCount }) { VerifyBatch(CreateBatch(count)); }
        Console.WriteLine("All serializer round trips, CBOR cross-reads, independent model checks, and payload checks passed.");
    }

    internal static Dictionary<string, byte[]> VerifyArray(int[] value)
    {
        var payloads = Encode(value, SystemFormatsCodec.Encode);
        VerifyOwn(payloads, decoded => EqualArray(value, decoded), SystemFormatsCodec.DecodeArray);
        foreach (var pair in payloads.Where(static pair => pair.Key != MessagePackName))
        {
            EqualArray(value, Codecs.CborDecode<int[]>(pair.Value));
            EqualArray(value, Codecs.RedoxDecode<int[]>(pair.Value));
            EqualArray(value, Codecs.PeterDecode<int[]>(pair.Value));
            EqualArray(value, SystemFormatsCodec.DecodeArray(pair.Value));
        }
        RequireEqualBytes(payloads[CborName], payloads[SystemFormatsName]);
        PrintSizes("IntegerArray/1024", payloads);
        return payloads;
    }

    internal static Dictionary<string, byte[]> VerifyMap(Dictionary<string, int> value)
    {
        var payloads = Encode(value, SystemFormatsCodec.Encode);
        VerifyOwn(payloads, decoded => EqualMap(value, decoded), SystemFormatsCodec.DecodeMap);
        foreach (var pair in payloads.Where(static pair => pair.Key != MessagePackName))
        {
            EqualMap(value, Codecs.CborDecode<Dictionary<string, int>>(pair.Value));
            EqualMap(value, Codecs.RedoxDecode<Dictionary<string, int>>(pair.Value));
            EqualMap(value, Codecs.PeterDecode<Dictionary<string, int>>(pair.Value));
            EqualMap(value, SystemFormatsCodec.DecodeMap(pair.Value));
        }
        RequireEqualBytes(payloads[CborName], payloads[SystemFormatsName]);
        PrintSizes("StringMap/1024", payloads);
        return payloads;
    }

    internal static Dictionary<string, byte[]> VerifyBatch(OrderBatch value)
    {
        var payloads = Encode(value, SystemFormatsCodec.Encode);
        VerifyOwn(payloads, decoded => EqualBatch(value, decoded), SystemFormatsCodec.DecodeBatch);
        foreach (var pair in payloads.Where(static pair => pair.Key != MessagePackName))
        {
            EqualBatch(value, SystemFormatsCodec.DecodeBatch(pair.Value));
        }
        // The reference adapter intentionally uses the same integer-map schema as generated Cbor.
        RequireEqualBytes(payloads[CborName], payloads[SystemFormatsName]);
        EqualBatch(value, Codecs.CborDecode<OrderBatch>(payloads[SystemFormatsName]));
        PrintSizes("NestedModel/" + value.Orders.Count.ToString(CultureInfo.InvariantCulture), payloads);
        return payloads;
    }

    private static Dictionary<string, byte[]> Encode<T>(T value, Func<T, byte[]> systemEncode) => new(StringComparer.Ordinal)
    {
        [CborName] = Codecs.CborEncode(value),
        [MessagePackName] = Codecs.MessagePackEncode(value),
        [RedoxName] = Codecs.RedoxEncode(value),
        [SystemFormatsName] = systemEncode(value),
        [PeterName] = Codecs.PeterEncode(value),
    };

    private static void VerifyOwn<T>(Dictionary<string, byte[]> payloads, Action<T> assert, Func<byte[], T> systemDecode)
    {
        assert(Codecs.CborDecode<T>(payloads[CborName]));
        assert(Codecs.MessagePackDecode<T>(payloads[MessagePackName]));
        assert(Codecs.RedoxDecode<T>(payloads[RedoxName]));
        assert(systemDecode(payloads[SystemFormatsName]));
        assert(Codecs.PeterDecode<T>(payloads[PeterName]));
    }

}
