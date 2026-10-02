using BenchmarkDotNet.Attributes;
using SerializerFoundation;

namespace Cbor.Benchmarks;

[MemoryDiagnoser]
[DisassemblyDiagnoser(maxDepth: 3)]
public class ScalarReadBenchmarks
{
    private byte[] encoded = [];

    [Params(23UL, 1000UL, ulong.MaxValue)]
    public ulong Value { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        byte[] bytes = new byte[9];
        CborPrimitives.TryWriteUInt64(bytes, Value, out int length);
        encoded = bytes.AsSpan(0, length).ToArray();
    }

    [Benchmark(Baseline = true)]
    public ulong GeneralHeader()
    {
        var buffer = new ReadOnlySpanReadBuffer(encoded);
        try
        {
            var header = buffer.PeekHeader();
            if (header.MajorType != CborMajorType.UnsignedInteger)
            {
                throw new InvalidDataException();
            }

            buffer.Advance(header.EncodedLength);
            return header.Argument;
        }
        finally { buffer.Dispose(); }
    }

    [Benchmark]
    public ulong Specialized()
    {
        var buffer = new ReadOnlySpanReadBuffer(encoded);
        try { return buffer.ReadUInt64(); }
        finally { buffer.Dispose(); }
    }
}
