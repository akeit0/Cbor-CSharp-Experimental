using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

namespace Cbor.Benchmarks.Comparison;

[MemoryDiagnoser, CategoriesColumn, GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class IntegerArrayComparison
{
    private int[] value = [];
    private byte[] cborPayload = [];
    private byte[] messagePackPayload = [];
    private byte[] redoxPayload = [];
    private byte[] systemPayload = [];
    private byte[] peterPayload = [];

    [GlobalSetup]
    public void Setup()
    {
        Codecs.VerifyAssets();
        value = Fixtures.CreateArray();
        var payloads = ComparisonVerification.VerifyArray(value);
        cborPayload = payloads[Fixtures.CborName];
        messagePackPayload = payloads[Fixtures.MessagePackName];
        redoxPayload = payloads[Fixtures.RedoxName];
        systemPayload = payloads[Fixtures.SystemFormatsName];
        peterPayload = payloads[Fixtures.PeterName];
    }

    [Benchmark(Baseline = true), BenchmarkCategory("Encode")] public byte[] CborEncode() => Codecs.CborEncode(value);
    [Benchmark, BenchmarkCategory("Encode")] public byte[] MessagePackV4Encode() => Codecs.MessagePackEncode(value);
    [Benchmark, BenchmarkCategory("Encode")] public byte[] REDoxEncode() => Codecs.RedoxEncode(value);
    [Benchmark, BenchmarkCategory("Encode")] public byte[] SystemFormatsEncode() => SystemFormatsCodec.Encode(value);
    [Benchmark, BenchmarkCategory("Encode")] public byte[] PeterOEncode() => Codecs.PeterEncode(value);
    [Benchmark(Baseline = true), BenchmarkCategory("Decode")] public int[] CborDecode() => Codecs.CborDecode<int[]>(cborPayload);
    [Benchmark, BenchmarkCategory("Decode")] public int[] MessagePackV4Decode() => Codecs.MessagePackDecode<int[]>(messagePackPayload);
    [Benchmark, BenchmarkCategory("Decode")] public int[] REDoxDecode() => Codecs.RedoxDecode<int[]>(redoxPayload);
    [Benchmark, BenchmarkCategory("Decode")] public int[] SystemFormatsDecode() => SystemFormatsCodec.DecodeArray(systemPayload);
    [Benchmark, BenchmarkCategory("Decode")] public int[] PeterODecode() => Codecs.PeterDecode<int[]>(peterPayload);
}

[MemoryDiagnoser, CategoriesColumn, GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class StringMapComparison
{
    private Dictionary<string, int> value = null!;
    private byte[] cborPayload = [];
    private byte[] messagePackPayload = [];
    private byte[] redoxPayload = [];
    private byte[] systemPayload = [];
    private byte[] peterPayload = [];

    [GlobalSetup]
    public void Setup()
    {
        Codecs.VerifyAssets();
        value = Fixtures.CreateMap();
        var payloads = ComparisonVerification.VerifyMap(value);
        cborPayload = payloads[Fixtures.CborName];
        messagePackPayload = payloads[Fixtures.MessagePackName];
        redoxPayload = payloads[Fixtures.RedoxName];
        systemPayload = payloads[Fixtures.SystemFormatsName];
        peterPayload = payloads[Fixtures.PeterName];
    }

    [Benchmark(Baseline = true), BenchmarkCategory("Encode")] public byte[] CborEncode() => Codecs.CborEncode(value);
    [Benchmark, BenchmarkCategory("Encode")] public byte[] MessagePackV4Encode() => Codecs.MessagePackEncode(value);
    [Benchmark, BenchmarkCategory("Encode")] public byte[] REDoxEncode() => Codecs.RedoxEncode(value);
    [Benchmark, BenchmarkCategory("Encode")] public byte[] SystemFormatsEncode() => SystemFormatsCodec.Encode(value);
    [Benchmark, BenchmarkCategory("Encode")] public byte[] PeterOEncode() => Codecs.PeterEncode(value);
    [Benchmark(Baseline = true), BenchmarkCategory("Decode")] public Dictionary<string, int> CborDecode() => Codecs.CborDecode<Dictionary<string, int>>(cborPayload);
    [Benchmark, BenchmarkCategory("Decode")] public Dictionary<string, int> MessagePackV4Decode() => Codecs.MessagePackDecode<Dictionary<string, int>>(messagePackPayload);
    [Benchmark, BenchmarkCategory("Decode")] public Dictionary<string, int> REDoxDecode() => Codecs.RedoxDecode<Dictionary<string, int>>(redoxPayload);
    [Benchmark, BenchmarkCategory("Decode")] public Dictionary<string, int> SystemFormatsDecode() => SystemFormatsCodec.DecodeMap(systemPayload);
    [Benchmark, BenchmarkCategory("Decode")] public Dictionary<string, int> PeterODecode() => Codecs.PeterDecode<Dictionary<string, int>>(peterPayload);
}

[MemoryDiagnoser, CategoriesColumn, GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class NestedModelComparison
{
    private OrderBatch value = null!;
    private byte[] cborPayload = [];
    private byte[] messagePackPayload = [];
    private byte[] redoxPayload = [];
    private byte[] systemPayload = [];
    private byte[] peterPayload = [];

    [Params(Fixtures.SmallOrderCount, Fixtures.LargeOrderCount)] public int OrderCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        Codecs.VerifyAssets();
        value = Fixtures.CreateBatch(OrderCount);
        var payloads = ComparisonVerification.VerifyBatch(value);
        cborPayload = payloads[Fixtures.CborName];
        messagePackPayload = payloads[Fixtures.MessagePackName];
        redoxPayload = payloads[Fixtures.RedoxName];
        systemPayload = payloads[Fixtures.SystemFormatsName];
        peterPayload = payloads[Fixtures.PeterName];
    }

    [Benchmark(Baseline = true), BenchmarkCategory("Encode")] public byte[] CborEncode() => Codecs.CborEncode(value);
    [Benchmark, BenchmarkCategory("Encode")] public byte[] MessagePackV4Encode() => Codecs.MessagePackEncode(value);
    [Benchmark, BenchmarkCategory("Encode")] public byte[] REDoxEncode() => Codecs.RedoxEncode(value);
    [Benchmark, BenchmarkCategory("Encode")] public byte[] SystemFormatsEncode() => SystemFormatsCodec.Encode(value);
    [Benchmark, BenchmarkCategory("Encode")] public byte[] PeterOEncode() => Codecs.PeterEncode(value);
    [Benchmark(Baseline = true), BenchmarkCategory("Decode")] public OrderBatch CborDecode() => Codecs.CborDecode<OrderBatch>(cborPayload);
    [Benchmark, BenchmarkCategory("Decode")] public OrderBatch MessagePackV4Decode() => Codecs.MessagePackDecode<OrderBatch>(messagePackPayload);
    [Benchmark, BenchmarkCategory("Decode")] public OrderBatch REDoxDecode() => Codecs.RedoxDecode<OrderBatch>(redoxPayload);
    [Benchmark, BenchmarkCategory("Decode")] public OrderBatch SystemFormatsDecode() => SystemFormatsCodec.DecodeBatch(systemPayload);
    [Benchmark, BenchmarkCategory("Decode")] public OrderBatch PeterODecode() => Codecs.PeterDecode<OrderBatch>(peterPayload);
}
