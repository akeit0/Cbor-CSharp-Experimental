using System.Buffers;
using BenchmarkDotNet.Attributes;
using Cbor;

namespace Cbor.Benchmarks;

[MemoryDiagnoser]
public class RuntimePathBenchmarks
{
    private static readonly CborSerializerOptions Options = new(RuntimePathResolver.Instance);
    private readonly RepeatedMembers model = new() { A = 1, B = 2, C = 3, D = 4, E = 5, F = 6, G = 7, H = 8 };
    private byte[] encodedModel = [];
    private byte[] integerMap = [];
    private byte[] stringMap = [];
    private byte[] ascii = [];
    private byte[] unicode = [];
    private ReadOnlySequence<byte> segmentedUnicode;

    [GlobalSetup]
    public void Setup()
    {
        encodedModel = CborSerializer.Serialize(model, Options);
        integerMap = CborSerializer.Serialize(Enumerable.Range(0, 1024).ToDictionary(static i => i, static i => i), Options);
        stringMap = CborSerializer.Serialize(Enumerable.Range(0, 1024).ToDictionary(
            static i => i.ToString(System.Globalization.CultureInfo.InvariantCulture), static i => i), Options);
        ascii = CborSerializer.Serialize(new string('a', 49_152));
        unicode = CborSerializer.Serialize(string.Concat(Enumerable.Repeat("水😀", 8192)));
        Segment? first = null;
        Segment? last = null;
        for (int offset = 0; offset < unicode.Length; offset += 4093)
        {
            var next = new Segment(unicode.AsMemory(offset, Math.Min(4093, unicode.Length - offset)));
            if (last is null) { first = next; }
            else { last.Append(next); }
            last = next;
        }

        segmentedUnicode = new ReadOnlySequence<byte>(first!, 0, last!, last!.Memory.Length);
        if (CborSerializer.Deserialize<Dictionary<int, int>>(integerMap, Options).Count != 1024 ||
            CborSerializer.Deserialize<Dictionary<string, int>>(stringMap, Options).Count != 1024 ||
            CborValidation.TryValidate(in segmentedUnicode) != CborDecodeResult.Success)
        {
            throw new InvalidOperationException("Benchmark fixture failed.");
        }
    }

    [Benchmark] public byte[] EncodeRepeatedMembers() => CborSerializer.Serialize(model, Options);
    [Benchmark] public RepeatedMembers DecodeRepeatedMembers() => CborSerializer.Deserialize<RepeatedMembers>(encodedModel, Options);
    [Benchmark] public Dictionary<int, int> DecodeIntegerMap() => CborSerializer.Deserialize<Dictionary<int, int>>(integerMap, Options);
    [Benchmark] public Dictionary<string, int> DecodeStringMap() => CborSerializer.Deserialize<Dictionary<string, int>>(stringMap, Options);
    [Benchmark] public CborDecodeResult ValidateAscii() => CborValidation.TryValidate(ascii);
    [Benchmark] public CborDecodeResult ValidateUnicode() => CborValidation.TryValidate(unicode);
    [Benchmark] public CborDecodeResult ValidateSegmentedUnicode() => CborValidation.TryValidate(in segmentedUnicode);

    private sealed class Segment : ReadOnlySequenceSegment<byte>
    {
        internal Segment(ReadOnlyMemory<byte> memory) => Memory = memory;

        // Benchmark construction is outside the measured operation.
        internal void Append(Segment next) { next.RunningIndex = RunningIndex + Memory.Length; Next = next; }
    }
}

[CborObject]
public sealed class RepeatedMembers
{
    [CborKey(0)] public int A { get; set; }
    [CborKey(1)] public int B { get; set; }
    [CborKey(2)] public int C { get; set; }
    [CborKey(3)] public int D { get; set; }
    [CborKey(4)] public int E { get; set; }
    [CborKey(5)] public int F { get; set; }
    [CborKey(6)] public int G { get; set; }
    [CborKey(7)] public int H { get; set; }
}

[CborResolver(typeof(RepeatedMembers), typeof(Dictionary<int, int>), typeof(Dictionary<string, int>))]
public partial class RuntimePathResolver;
