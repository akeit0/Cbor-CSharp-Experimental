using BenchmarkDotNet.Attributes;
using Cbor;
using SerializerFoundation;

namespace Cbor.Benchmarks;

[MemoryDiagnoser]
public class TypedPathBenchmarks
{
    private static readonly CborSerializerOptions Options = new(ReviewFactory.Instance);
    private string text = string.Empty;
    private int[] values = [];
    private byte[] encodedValues = [];
    private byte[] unknownMembers = [];

    [GlobalSetup]
    public void Setup()
    {
        text = new string('水', 16_384);
        values = Enumerable.Range(0, 1024).ToArray();
        encodedValues = CborSerializer.Serialize(values, Options);
        Span<byte> scratch = stackalloc byte[256];
        var writer = new ArrayPoolListWriteBuffer(scratch);
        try
        {
            writer.WriteMapHeader(65);
            writer.WriteUInt64(0);
            writer.WriteUInt64(42);
            for (ulong key = 1; key <= 64; key++)
            {
                writer.WriteUInt64(key);
                writer.WriteUInt64(key);
            }

            unknownMembers = writer.ToArray();
        }
        finally
        {
            writer.Dispose();
        }

        if (CborSerializer.Deserialize<ReviewModel>(unknownMembers, Options).Value != 42)
        {
            throw new InvalidOperationException("Benchmark fixture failed.");
        }
    }

    [Benchmark]
    public byte[] EncodeUtf8Text() => CborSerializer.Serialize(text);

    [Benchmark]
    public byte[] EncodeIntegerArray() => CborSerializer.Serialize(values, Options);

    [Benchmark]
    public int[] DecodeIntegerArray() => CborSerializer.Deserialize<int[]>(encodedValues, Options);

    [Benchmark]
    public ReviewModel DecodeUnknownMembers() => CborSerializer.Deserialize<ReviewModel>(unknownMembers, Options);
}

[CborObject]
public sealed class ReviewModel
{
    [CborKey(0, Required = true)]
    public int Value { get; set; }
}

[CborFactory(typeof(ReviewModel), typeof(int[]))]
public partial class ReviewFactory;
