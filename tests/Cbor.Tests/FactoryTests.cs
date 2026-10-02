using SerializerFoundation;

namespace Cbor.Tests;

public sealed class FactoryTests
{
    [Fact]
    public void InitializedSelectionIsPerResolverAndSharedAcrossConcurrentOperations()
    {
        var factory = new CountingFactory();
        var first = new CborFormatterResolver(factory);
        var choices = new ICborFormatter<CompatibleArrayPoolListWriteBuffer, CompatibleReadOnlySpanReadBuffer, int>[100];
        Parallel.For(0, choices.Length, i => choices[i] = Get(first));
        Assert.All(choices, choice => Assert.Same(choices[0], choice));
        Assert.Equal(1, factory.Calls);
        Assert.Equal(1, factory.Initializations);
        Assert.NotSame(choices[0], Get(new CborFormatterResolver(factory)));
        Assert.Equal(2, factory.Calls);
        Assert.Equal(2, factory.Initializations);
        var options = new CborSerializerOptions(true, first);
        Parallel.For(0, 100, i => Assert.Equal(i, CborSerializer.Deserialize<int>(CborSerializer.Serialize(i, options), options)));
        // Options append built-ins in a new resolver so dependencies see the complete provider chain.
        Assert.Equal(3, factory.Calls);
        Assert.Equal(3, factory.Initializations);
    }

    [Fact]
    public void UnsupportedSelectionsAreCachedAndCreationFailuresCanRetry()
    {
        var factory = new CountingFactory();
        var resolver = factory.CreateResolver();
        Assert.Same(resolver.GetFormatter<CompatibleArrayPoolListWriteBuffer, CompatibleReadOnlySpanReadBuffer, Version>(),
            resolver.GetFormatter<CompatibleArrayPoolListWriteBuffer, CompatibleReadOnlySpanReadBuffer, Version>());
        Assert.Equal(1, factory.Calls);
        factory.Fail = true;
        Assert.Throws<InvalidOperationException>(() => Get(resolver));
        factory.Fail = false;
        Assert.NotNull(Get(resolver));
        Assert.Equal(3, factory.Calls);
    }

    [Fact]
    public void InitializationFailureNeverPublishesAndCanRetry()
    {
        var factory = new CountingFactory { FailInitialize = true };
        var resolver = factory.CreateResolver();
        Assert.Throws<InvalidOperationException>(() => Get(resolver));
        factory.FailInitialize = false;
        var formatter = Get(resolver);
        Assert.Same(formatter, Get(resolver));
        Assert.Equal(2, factory.Calls);
        Assert.Equal(2, factory.Initializations);
    }

    [Fact]
    public void FactoryCompositionCopiesInputAndKeepsFirstSelection()
    {
        var factory = new CountingFactory();
        CborFormatterFactory[] input = [factory, CborFormatterFactory.Builtin];
        var combined = CborFormatterFactory.Combine(input);
        input[0] = CborFormatterFactory.Builtin;
        var resolver = combined.CreateResolver();
        Assert.IsType<TestFormatter<CompatibleArrayPoolListWriteBuffer, CompatibleReadOnlySpanReadBuffer>>(Get(resolver));
        Assert.NotNull(resolver.GetFormatter<CompatibleArrayPoolListWriteBuffer, CompatibleReadOnlySpanReadBuffer, string>());
        Assert.Equal(2, factory.Calls);
        Assert.Throws<ArgumentNullException>(() => new CborFormatterResolver(null!));
        Assert.Throws<ArgumentNullException>(() => CborFormatterFactory.FromResolver(null!));
        Assert.Throws<ArgumentNullException>(() => CborFormatterFactory.Combine(null!));
        Assert.Throws<ArgumentException>(() => CborFormatterFactory.Combine([null!]));
    }

    [Fact]
    public void RecursiveGeneratedGraphsInitializeWithoutCyclesAndPreserveChildOverrides()
    {
        var resolver = CborFormatterFactory.Combine(CborFormatterFactory.FromResolver(TestResolver.Instance), CborFormatterFactory.Builtin).CreateResolver();
        var options = new CborSerializerOptions(resolver);
        var tree = new TreeNode { Value = 7, Children = [new TreeNode { Value = 9 }] };
        var decoded = CborSerializer.Deserialize<TreeNode>(CborSerializer.Serialize(tree, options), options);
        Assert.Equal(7, decoded.Value);
        Assert.Equal(9, decoded.Children![0].Value);
    }

    [Fact]
    public async Task CyclicInitializationPublishesOnlyAfterTheEntireGraphCompletes()
    {
        using var entered = new ManualResetEventSlim();
        using var proceed = new ManualResetEventSlim();
        var factory = new GraphFactory(entered, proceed);
        var resolver = factory.CreateResolver();
        var rootTask = Task.Run(() => resolver.GetFormatter<CompatibleArrayPoolListWriteBuffer, CompatibleReadOnlySpanReadBuffer, GraphRoot>());
        Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
        var childTask = Task.Run(() => resolver.GetFormatter<CompatibleArrayPoolListWriteBuffer, CompatibleReadOnlySpanReadBuffer, GraphChild>());
        try
        {
            await Task.Delay(50);
            Assert.False(childTask.IsCompleted);
        }
        finally { proceed.Set(); }
        var root = Assert.IsType<RootFormatter>(await rootTask);
        var child = Assert.IsType<ChildFormatter>(await childTask);
        Assert.Same(root.Child, child);
        Assert.Same(root, child.Root);
        Assert.True(root.Ready);
        Assert.True(child.Ready);
    }

    private sealed class GraphRoot;
    private sealed class GraphChild;
    private sealed class GraphFactory(ManualResetEventSlim entered, ManualResetEventSlim proceed) : CborFormatterFactory
    {
        public bool FailChild;
        public bool SwallowChildFailure;
        public int Creations;
        public override object? CreateFormatter(Type writeBufferType, Type readBufferType, Type valueType)
        {
            if (writeBufferType != typeof(CompatibleArrayPoolListWriteBuffer) || readBufferType != typeof(CompatibleReadOnlySpanReadBuffer)) { return null; }
            Interlocked.Increment(ref Creations);
            if (valueType == typeof(GraphRoot)) { return new RootFormatter(entered, proceed, this); }
            if (valueType == typeof(GraphChild)) { return new ChildFormatter(this); }
            return null;
        }
    }
    private sealed class RootFormatter(ManualResetEventSlim entered, ManualResetEventSlim proceed, GraphFactory factory) : ICborFormatter<CompatibleArrayPoolListWriteBuffer, CompatibleReadOnlySpanReadBuffer, GraphRoot>
    {
        public ICborFormatter<CompatibleArrayPoolListWriteBuffer, CompatibleReadOnlySpanReadBuffer, GraphChild> Child = null!;
        public bool Ready;
        public void Initialize(CborFormatterResolver resolver)
        {
            try { Child = resolver.GetFormatter<CompatibleArrayPoolListWriteBuffer, CompatibleReadOnlySpanReadBuffer, GraphChild>(); }
            catch (InvalidOperationException) when (factory.SwallowChildFailure) { }
            entered.Set();
            if (!proceed.Wait(TimeSpan.FromSeconds(5))) { throw new InvalidOperationException("Graph publication test timed out."); }
            Ready = true;
        }
        public void Serialize(ref CompatibleArrayPoolListWriteBuffer buffer, ref CborSerializationContext context, GraphRoot value) => throw new NotSupportedException();
        public GraphRoot Deserialize(ref CompatibleReadOnlySpanReadBuffer buffer, ref CborDeserializationContext context) => throw new NotSupportedException();
    }
    private sealed class ChildFormatter(GraphFactory factory) : ICborFormatter<CompatibleArrayPoolListWriteBuffer, CompatibleReadOnlySpanReadBuffer, GraphChild>
    {
        public ICborFormatter<CompatibleArrayPoolListWriteBuffer, CompatibleReadOnlySpanReadBuffer, GraphRoot> Root = null!;
        public bool Ready;
        public void Initialize(CborFormatterResolver resolver)
        {
            Root = resolver.GetFormatter<CompatibleArrayPoolListWriteBuffer, CompatibleReadOnlySpanReadBuffer, GraphRoot>();
            if (factory.FailChild) { throw new InvalidOperationException("Child initialization failed."); }
            Ready = true;
        }
        public void Serialize(ref CompatibleArrayPoolListWriteBuffer buffer, ref CborSerializationContext context, GraphChild value) => throw new NotSupportedException();
        public GraphChild Deserialize(ref CompatibleReadOnlySpanReadBuffer buffer, ref CborDeserializationContext context) => throw new NotSupportedException();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FailedDependencyAbandonsTheWholeGraphEvenWhenItsParentSwallowsTheException(bool swallow)
    {
        using var entered = new ManualResetEventSlim();
        using var proceed = new ManualResetEventSlim(true);
        var factory = new GraphFactory(entered, proceed) { FailChild = true, SwallowChildFailure = swallow };
        var resolver = factory.CreateResolver();
        Assert.Throws<InvalidOperationException>(() => resolver.GetFormatter<CompatibleArrayPoolListWriteBuffer, CompatibleReadOnlySpanReadBuffer, GraphRoot>());
        factory.FailChild = false;
        var root = Assert.IsType<RootFormatter>(resolver.GetFormatter<CompatibleArrayPoolListWriteBuffer, CompatibleReadOnlySpanReadBuffer, GraphRoot>());
        var child = Assert.IsType<ChildFormatter>(root.Child);
        Assert.Same(root, child.Root);
        Assert.Equal(4, factory.Creations);
        Assert.True(root.Ready);
        Assert.True(child.Ready);
    }

    private static ICborFormatter<CompatibleArrayPoolListWriteBuffer, CompatibleReadOnlySpanReadBuffer, int> Get(CborFormatterResolver resolver)
        => resolver.GetFormatter<CompatibleArrayPoolListWriteBuffer, CompatibleReadOnlySpanReadBuffer, int>();

    private sealed class CountingFactory : CborFormatterFactory
    {
        public int Calls;
        public int Initializations;
        public bool Fail;
        public bool FailInitialize;
        public override object? CreateFormatter(Type writeBufferType, Type readBufferType, Type valueType)
        {
            Interlocked.Increment(ref Calls);
            if (Fail) { throw new InvalidOperationException("Creation failure."); }
            if (valueType != typeof(int)) { return null; }
            if (writeBufferType == typeof(CompatibleArrayPoolListWriteBuffer) && readBufferType == typeof(CompatibleReadOnlySpanReadBuffer))
            {
                return new TestFormatter<CompatibleArrayPoolListWriteBuffer, CompatibleReadOnlySpanReadBuffer>(this);
            }
            return null;
        }
    }

    private sealed class TestFormatter<W, R>(CountingFactory factory) : ICborFormatter<W, R, int>
        where W : struct, IWriteBuffer
        where R : struct, IReadBuffer
    {
        public void Initialize(CborFormatterResolver resolver)
        {
            Interlocked.Increment(ref factory.Initializations);
            if (factory.FailInitialize) { throw new InvalidOperationException("Initialization failure."); }
        }
        public void Serialize(ref W buffer, ref CborSerializationContext context, int value) => buffer.WriteInt64(value);
        public int Deserialize(ref R buffer, ref CborDeserializationContext context) => checked((int)buffer.ReadInt64());
    }
}
