using System.Collections.Concurrent;
using SerializerFoundation;

namespace Cbor.Tests;

public sealed class OperationResolutionTests
{
    [Fact]
    public void RecursiveRepeatedModelsResolveOnceForEachBufferPairAcrossOperations()
    {
        var resolver = new CountingResolver();
        var options = new CborSerializerOptions(resolver);
        var root = new TreeNode { Value = 1, Children = [] };
        for (int i = 0; i < 32; i++)
        {
            root.Children.Add(new TreeNode { Value = i, Children = [new TreeNode { Value = i + 1 }] });
        }
        byte[] bytes = CborSerializer.Serialize(root, options);
        AssertCounts(resolver, 1);
        Assert.Equal(32, CborSerializer.Deserialize<TreeNode>(bytes, options).Children!.Count);
        AssertCounts(resolver, 1);
        for (int i = 0; i < 20; i++)
        {
            Assert.Equal(bytes, CborSerializer.Serialize(root, options));
            Assert.Equal(32, CborSerializer.Deserialize<TreeNode>(bytes, options).Children!.Count);
        }
        AssertCounts(resolver, 1);
    }

    [Fact]
    public void ConcurrentOperationsShareOnlyCompletedFormatterGraphs()
    {
        var resolver = new CountingResolver();
        var options = new CborSerializerOptions(resolver);
        Parallel.For(0, 128, i =>
        {
            var value = new TreeNode { Value = i, Children = [new TreeNode { Value = i }] };
            byte[] bytes = CborSerializer.Serialize(value, options);
            Assert.Equal(i, CborSerializer.Deserialize<TreeNode>(bytes, options).Children![0].Value);
        });
        AssertCounts(resolver, 1);
    }

    [Fact]
    public void ImmutableResolverSelectionsRequireNewOptionsToChangeOverrides()
    {
        var resolver = new SwitchingResolver();
        var options = new CborSerializerOptions(resolver);
        var root = new TreeNode { Value = 1, Children = [new TreeNode { Value = 2 }] };
        byte[] plain = CborSerializer.Serialize(root, options);
        resolver.UseOffset = true;
        Assert.Equal(plain, CborSerializer.Serialize(root, options));
        var alternate = new CborSerializerOptions(resolver);
        byte[] shifted = CborSerializer.Serialize(root, alternate);
        Assert.NotEqual(plain, shifted);
        Assert.Equal(2, CborSerializer.Deserialize<TreeNode>(shifted, alternate).Children![0].Value);
        Assert.Equal(plain, CborSerializer.Serialize(root, options));
    }

    [Fact]
    public void WarmGraphResolutionAndBorrowedScalarWritesAllocateNoMemory()
    {
        var options = new CborSerializerOptions(TestResolver.Instance);
        var writer = new System.Buffers.ArrayBufferWriter<byte>();
        CborSerializer.Serialize(writer, 1, options);
        writer.Clear();
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 128; i++) { CborSerializer.Serialize(writer, i, options); writer.Clear(); }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void DirectLegacyCollectionCallsPreserveContextOverridesAndItemBudgets()
    {
        var options = new CborSerializerOptions(new CborFormatterRegistry().Add(new OffsetFormatter()).Build(),
            new CborReaderOptions(maxItems: 3));
        var output = new System.Buffers.ArrayBufferWriter<byte>();
        var writer = new CompatibleBufferWriterWriteBuffer(output);
        var writeContext = new CborSerializationContext(options);
        int[] values = [1, 2];
        try
        {
            writeContext.Serialize(ref writer, values, new CborArrayFormatter<int>());
            writer.Flush();
            Assert.Equal("820B0C", Convert.ToHexString(output.WrittenSpan));
        }
        finally { writeContext.Dispose(); writer.Dispose(); }
        var sequence = new System.Buffers.ReadOnlySequence<byte>(output.WrittenMemory);
        var reader = new CompatibleReadOnlySequenceReadBuffer(in sequence);
        var readContext = new CborDeserializationContext(options, sequence.Length);
        try
        {
            Assert.Equal(values, Assert.IsType<int[]>(readContext.Deserialize(ref reader, new CborArrayFormatter<int>())));
            Assert.Equal(0, reader.BytesRemaining);
        }
        finally { readContext.Dispose(); reader.Dispose(); }
    }

    private static void AssertCounts(CountingResolver resolver, int count)
    {
        Assert.Equal(count, resolver.Count<TreeNode>());
        Assert.Equal(count, resolver.Count<List<TreeNode>>());
        Assert.Equal(count, resolver.Count<int>());
    }

    private sealed class CountingResolver : CborFormatterResolver
    {
        private readonly ConcurrentDictionary<Type, int> counts = new();
        public int Count<T>() => counts.TryGetValue(typeof(T), out int count) ? count : 0;
        public override ICborFormatter<T>? GetFormatter<T>()
        {
            counts.AddOrUpdate(typeof(T), 1, static (_, count) => count + 1);
            return TestResolver.Instance.GetFormatter<T>();
        }
    }
    private sealed class SwitchingResolver : CborFormatterResolver
    {
        public bool UseOffset { get; set; }
        public override ICborFormatter<T>? GetFormatter<T>() => typeof(T) == typeof(int) && UseOffset
            ? (ICborFormatter<T>)(object)new OffsetFormatter() : TestResolver.Instance.GetFormatter<T>();
    }
    private sealed class OffsetFormatter : ICborFormatter<int>
    {
        public void Serialize<W>(ref W buffer, ref CborSerializationContext context, int value) where W : struct, IWriteBuffer => buffer.WriteInt64(value + 10);
        public int Deserialize<R>(ref R buffer, ref CborDeserializationContext context) where R : struct, IReadBuffer => checked((int)buffer.ReadInt64() - 10);
    }
}
