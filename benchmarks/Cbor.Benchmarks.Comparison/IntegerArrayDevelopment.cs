using BenchmarkDotNet.Attributes;
using Cbor.Benchmarks.Comparison;

namespace Cbor.Benchmarks.Development;

// Small controls for the development loop; excluded by the five-library '*Comparison*' filter.
[MemoryDiagnoser]
public class IntegerArrayDevelopment
{
    private int[] value = [];
    private byte[] payload = [];

    [Params(1, 8, 1024)] public int Count { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        Codecs.VerifyAssets();
        value = Fixtures.CreateArray().AsSpan(0, Count).ToArray();
        payload = Codecs.CborEncode(value);
        Fixtures.EqualArray(value, Codecs.CborDecode<int[]>(payload));
    }

    [Benchmark] public byte[] Encode() => Codecs.CborEncode(value);
    [Benchmark] public int[] Decode() => Codecs.CborDecode<int[]>(payload);
}
