using System.Collections.Concurrent;
using SerializerFoundation;

namespace Cbor.Tests;

public sealed class OperationResolutionTests
{
    [Fact]
    public void RecursiveRepeatedModelsResolveOnceForEachBufferPairAcrossOperations()
    {
        var resolver = new CountingFactory();
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
        var resolver = new CountingFactory();
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
        var resolver = new SwitchingFactory();
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
        var options = new CborSerializerOptions(ModelFactory.Instance);
        var writer = new System.Buffers.ArrayBufferWriter<byte>();
        CborSerializer.Serialize(writer, 1, options);
        writer.Clear();
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 128; i++) { CborSerializer.Serialize(writer, i, options); writer.Clear(); }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    private static void AssertCounts(CountingFactory resolver, int count)
    {
        Assert.Equal(count, resolver.Count<TreeNode>());
        Assert.Equal(count, resolver.Count<List<TreeNode>>());
        Assert.Equal(count, resolver.Count<int>());
    }

    private sealed class CountingFactory : TestFactory
    {
        private readonly ConcurrentDictionary<Type, int> counts = new();
        public int Count<T>() => counts.TryGetValue(typeof(T), out int count) ? count : 0;
        protected override object? Create<W, R>(Type valueType)
        {
            counts.AddOrUpdate(valueType, 1, static (_, count) => count + 1);
            return From<W, R>(ModelFactory.Instance, valueType);
        }
    }
    private sealed class SwitchingFactory : TestFactory
    {
        public bool UseOffset { get; set; }
        protected override object? Create<W, R>(Type valueType) => UseOffset && valueType == typeof(int)
            ? From<W, R>(new OffsetFactory(10), valueType) : From<W, R>(ModelFactory.Instance, valueType);
    }
}
