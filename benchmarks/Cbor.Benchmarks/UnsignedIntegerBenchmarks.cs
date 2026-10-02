using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using SerializerFoundation;
using System.Formats.Cbor;

namespace Cbor.Benchmarks;

// This measures the primitive buffer path, not whole-object serializer parity.
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class UnsignedIntegerBenchmarks
{
    private byte[] encoded = [];

    [Params(23UL, 1000UL, ulong.MaxValue)]
    public ulong Value { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        byte[] scratch = new byte[9];
        CborPrimitives.TryWriteUInt64(scratch, Value, out int length);
        encoded = scratch.AsSpan(0, length).ToArray();
    }

    [Benchmark]
    [BenchmarkCategory("Write")]
    public byte[] FoundationWrite()
    {
        Span<byte> scratch = stackalloc byte[16];
        var buffer = new ArrayPoolListWriteBuffer(scratch);
        try
        {
            buffer.WriteUInt64(Value);
            return buffer.ToArray();
        }
        finally
        {
            buffer.Dispose();
        }
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Write")]
    public byte[] BclWrite()
    {
        var writer = new CborWriter(CborConformanceMode.Strict);
        writer.WriteUInt64(Value);
        return writer.Encode();
    }

    [Benchmark]
    [BenchmarkCategory("Read")]
    public ulong FoundationRead()
    {
        var buffer = new ReadOnlySpanReadBuffer(encoded);
        try
        {
            return buffer.ReadUInt64();
        }
        finally
        {
            buffer.Dispose();
        }
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Read")]
    public ulong BclRead() => new CborReader(encoded, CborConformanceMode.Strict).ReadUInt64();
}
