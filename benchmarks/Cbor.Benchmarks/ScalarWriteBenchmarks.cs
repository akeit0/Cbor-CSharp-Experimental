using BenchmarkDotNet.Attributes;
using SerializerFoundation;

namespace Cbor.Benchmarks;

[MemoryDiagnoser]
[DisassemblyDiagnoser(maxDepth: 3)]
public class ScalarWriteBenchmarks
{
    [Params(23UL, 1000UL, ulong.MaxValue)]
    public ulong Value { get; set; }

    [Benchmark(Baseline = true)]
    public byte CheckedSpan()
    {
        Span<byte> scratch = stackalloc byte[16];
        var buffer = new SpanWriteBuffer(scratch);
        try
        {
            CborPrimitives.TryWriteUInt64(buffer.GetSpan(CborPrimitives.GetHeaderLength(Value)), Value, out int length);
            buffer.Advance(length);
            return scratch[(int)buffer.BytesWritten - 1];
        }
        finally { buffer.Dispose(); }
    }

    [Benchmark]
    public byte UnsafeBuffer()
    {
        Span<byte> scratch = stackalloc byte[16];
        var buffer = new SpanWriteBuffer(scratch);
        try
        {
            buffer.WriteUInt64(Value);
            return scratch[(int)buffer.BytesWritten - 1];
        }
        finally { buffer.Dispose(); }
    }
}
