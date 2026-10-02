using System.Buffers;
using System.Numerics;
using BenchmarkDotNet.Attributes;
using Cbor;

namespace Cbor.Benchmarks;

[MemoryDiagnoser]
public class WireValueBenchmarks
{
    private readonly ArrayBufferWriter<byte> output = new(1024);
    private BigInteger value;
    private byte[] encoded = [];
    private ReadOnlySequence<byte> fragmented;

    [Params(64, 65, 2048)]
    public int MagnitudeBits { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        RuntimeAssetCheck.Verify();
        value = (BigInteger.One << MagnitudeBits) - 1;
        encoded = CborSerializer.Serialize(value);
        Segment? first = null;
        Segment? last = null;
        for (int offset = 0; offset < encoded.Length; offset++)
        {
            var next = new Segment(encoded.AsMemory(offset, 1));
            if (last is null) { first = next; }
            else { last.Append(next); }
            last = next;
        }
        fragmented = new(first!, 0, last!, last!.Memory.Length);
        if (CborSerializer.Deserialize<BigInteger>(in fragmented) != value)
        {
            throw new InvalidOperationException("Bignum benchmark fixture failed.");
        }
        Console.WriteLine($"Magnitude bits: {MagnitudeBits}; payload bytes: {encoded.Length}.");
    }

    [Benchmark]
    public int EncodeToWriter()
    {
        output.Clear();
        CborSerializer.Serialize(output, value);
        return output.WrittenCount;
    }

    [Benchmark] public BigInteger DecodeContiguous() => CborSerializer.Deserialize<BigInteger>(encoded);
    [Benchmark] public BigInteger DecodeFragmented() => CborSerializer.Deserialize<BigInteger>(in fragmented);

    private sealed class Segment : ReadOnlySequenceSegment<byte>
    {
        internal Segment(ReadOnlyMemory<byte> memory) => Memory = memory;
        internal void Append(Segment next)
        {
            next.RunningIndex = RunningIndex + Memory.Length;
            Next = next;
        }
    }
}
