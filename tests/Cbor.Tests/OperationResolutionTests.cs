using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Cbor.Internal;
using SerializerFoundation;

namespace Cbor.Tests;

public sealed class OperationResolutionTests
{
    [Fact]
    public void RecursiveRepeatedModelsResolveEveryUsedTypeOncePerOperation()
    {
        var resolver = new CountingResolver();
        var options = new CborSerializerOptions(resolver);
        var root = new TreeNode { Value = 1, Children = [] };
        for (int i = 0; i < 32; i++)
        {
            root.Children.Add(new TreeNode { Value = i, Children = [new TreeNode { Value = i + 1 }] });
        }

        byte[] bytes = CborSerializer.Serialize(root, options);
        Assert.Equal(1, resolver.Count<TreeNode>());
        Assert.Equal(1, resolver.Count<List<TreeNode>>());
        Assert.Equal(1, resolver.Count<int>());
        Assert.Equal(32, CborSerializer.Deserialize<TreeNode>(bytes, options).Children!.Count);
        Assert.Equal(2, resolver.Count<TreeNode>());
        Assert.Equal(2, resolver.Count<List<TreeNode>>());
        Assert.Equal(2, resolver.Count<int>());
    }

    [Fact]
    public void SameOptionsObserveResolverChangesOnTheNextOperation()
    {
        var resolver = new SwitchingResolver();
        var options = new CborSerializerOptions(resolver);
        var root = new TreeNode { Value = 1, Children = [new TreeNode { Value = 2 }] };
        byte[] plain = CborSerializer.Serialize(root, options);
        Assert.Equal(1, resolver.IntegerResolutions);
        resolver.UseOffset = true;
        byte[] shifted = CborSerializer.Serialize(root, options);
        Assert.NotEqual(plain, shifted);
        Assert.Equal(2, resolver.IntegerResolutions);
        Assert.Equal(2, CborSerializer.Deserialize<TreeNode>(shifted, options).Children![0].Value);
        resolver.UseOffset = false;
        Assert.Equal(plain, CborSerializer.Serialize(root, options));
    }

    [Fact]
    public void ConcurrentOperationsDoNotShareResolutionState()
    {
        var resolver = new CountingResolver();
        var options = new CborSerializerOptions(resolver);
        Parallel.For(0, 128, i =>
        {
            var value = new TreeNode { Value = i, Children = [new TreeNode { Value = i }] };
            byte[] bytes = CborSerializer.Serialize(value, options);
            Assert.Equal(i, CborSerializer.Deserialize<TreeNode>(bytes, options).Children![0].Value);
        });
        Assert.Equal(256, resolver.Count<TreeNode>());
        Assert.Equal(256, resolver.Count<List<TreeNode>>());
        Assert.Equal(256, resolver.Count<int>());
    }

    [Fact]
    public void CacheKeysUseTheRequestedTypeEvenWhenAFormatterImplementsOtherTypes()
    {
        var multi = new MultiFormatter();
        var selectedLong = new UnusedFormatter<long>();
        var resolver = new CborFormatterRegistry().Add<int>(multi).Add<long>(selectedLong).Build();
        var cache = new OperationFormatterCache();
        try
        {
            Assert.Same(multi, cache.GetRequiredFormatter<int>(resolver));
            Assert.Same(selectedLong, cache.GetRequiredFormatter<long>(resolver));
            Assert.Same(multi, cache.GetRequiredFormatter<int>(resolver));
        }
        finally
        {
            cache.Dispose();
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void ResolverReentryPreservesNestedSelectionsAndTableGrowth(int seededTypes)
    {
        var cache = new OperationFormatterCache();
        var resolver = new ReentrantResolver();
        try
        {
            if (seededTypes > 0) { cache.GetRequiredFormatter<long>(resolver); }
            if (seededTypes > 1) { cache.GetRequiredFormatter<string>(resolver); }
            ICborFormatter<int>? nestedSelection = null;
            resolver.Callback = () =>
            {
                ResolveWide(ref cache, resolver);
                nestedSelection = cache.GetRequiredFormatter<int>(resolver);
            };
            var selection = cache.GetRequiredFormatter<int>(resolver);
            Assert.Same(nestedSelection, selection);
            int resolutions = resolver.Resolutions;
            ResolveWide(ref cache, resolver);
            Assert.Equal(resolutions, resolver.Resolutions);
            Assert.Same(selection, cache.GetRequiredFormatter<int>(resolver));
        }
        finally
        {
            cache.Dispose();
        }
    }

    [Fact]
    public void OverflowResolvesOnceGrowsAndDisposesIdempotently()
    {
        var resolver = new UniversalResolver();
        var cache = new OperationFormatterCache();
        try
        {
            ResolveWide(ref cache, resolver);
            ResolveWide(ref cache, resolver);
            Assert.Equal(25, resolver.Resolutions);
        }
        finally
        {
            cache.Dispose();
        }

        cache.Dispose();
        ResolveWide(ref cache, resolver);
        Assert.Equal(50, resolver.Resolutions);
        cache.Dispose();
    }

    [Fact]
    public void WarmInlineAndOverflowCachesAllocateNoMemoryPerOperation()
    {
        var resolver = new UniversalResolver();
        RunWide(resolver);
        RunWide(resolver);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 128; i++)
        {
            RunWide(resolver);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void FailedResolutionIsNotCachedAndExceptionIsUnchanged()
    {
        var resolver = new UniversalResolver { Failure = new InvalidOperationException("resolution failed") };
        var cache = new OperationFormatterCache();
        try
        {
            Assert.Same(resolver.Failure, Assert.Throws<InvalidOperationException>(() => cache.GetRequiredFormatter<int>(resolver)));
            resolver.Failure = null;
            Assert.Same(cache.GetRequiredFormatter<int>(resolver), cache.GetRequiredFormatter<int>(resolver));
            Assert.Equal(2, resolver.Resolutions);
        }
        finally
        {
            cache.Dispose();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DisposalReleasesInlineAndPooledFormatterReferences(bool abandon)
    {
        var (inline, overflow) = CreateDisposedFormatters(abandon);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.False(inline.IsAlive);
        Assert.False(overflow.IsAlive);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference Inline, WeakReference Overflow) CreateDisposedFormatters(bool abandon)
    {
        var resolver = new EphemeralResolver();
        var cache = new OperationFormatterCache();
        WeakReference inline = null!;
        WeakReference overflow = null!;
        try
        {
            try
            {
                ResolveWide(ref cache, resolver);
                inline = new WeakReference(cache.GetRequiredFormatter<byte>(resolver));
                overflow = new WeakReference(cache.GetRequiredFormatter<byte[][]>(resolver));
                if (abandon)
                {
                    throw new InvalidOperationException("abandon operation");
                }
            }
            finally
            {
                cache.Dispose();
            }
        }
        catch (InvalidOperationException) when (abandon)
        {
        }

        return (inline, overflow);
    }

    private static void RunWide(UniversalResolver resolver)
    {
        var cache = new OperationFormatterCache();
        try
        {
            ResolveWide(ref cache, resolver);
            ResolveWide(ref cache, resolver);
        }
        finally
        {
            cache.Dispose();
        }
    }

    private static void ResolveWide(ref OperationFormatterCache cache, CborFormatterResolver resolver)
    {
        cache.GetRequiredFormatter<byte>(resolver);
        cache.GetRequiredFormatter<sbyte>(resolver);
        cache.GetRequiredFormatter<short>(resolver);
        cache.GetRequiredFormatter<ushort>(resolver);
        cache.GetRequiredFormatter<int>(resolver);
        cache.GetRequiredFormatter<uint>(resolver);
        cache.GetRequiredFormatter<long>(resolver);
        cache.GetRequiredFormatter<ulong>(resolver);
        cache.GetRequiredFormatter<float>(resolver);
        cache.GetRequiredFormatter<double>(resolver);
        cache.GetRequiredFormatter<bool>(resolver);
        cache.GetRequiredFormatter<string>(resolver);
        cache.GetRequiredFormatter<byte[]>(resolver);
        cache.GetRequiredFormatter<sbyte[]>(resolver);
        cache.GetRequiredFormatter<short[]>(resolver);
        cache.GetRequiredFormatter<ushort[]>(resolver);
        cache.GetRequiredFormatter<int[]>(resolver);
        cache.GetRequiredFormatter<uint[]>(resolver);
        cache.GetRequiredFormatter<long[]>(resolver);
        cache.GetRequiredFormatter<ulong[]>(resolver);
        cache.GetRequiredFormatter<float[]>(resolver);
        cache.GetRequiredFormatter<double[]>(resolver);
        cache.GetRequiredFormatter<bool[]>(resolver);
        cache.GetRequiredFormatter<string[]>(resolver);
        cache.GetRequiredFormatter<byte[][]>(resolver);
    }

    private sealed class UniversalResolver : CborFormatterResolver
    {
        public int Resolutions { get; private set; }
        public Exception? Failure { get; set; }
        public override ICborFormatter<T> GetFormatter<T>()
        {
            Resolutions++;
            if (Failure is not null)
            {
                throw Failure;
            }

            return UnusedFormatter<T>.Instance;
        }
    }

    private sealed class EphemeralResolver : CborFormatterResolver
    {
        public override ICborFormatter<T> GetFormatter<T>() => new UnusedFormatter<T>();
    }

    private sealed class ReentrantResolver : CborFormatterResolver
    {
        public Action? Callback { get; set; }
        public int Resolutions { get; private set; }
        public override ICborFormatter<T> GetFormatter<T>()
        {
            Resolutions++;
            var callback = Callback;
            Callback = null;
            callback?.Invoke();
            return new UnusedFormatter<T>();
        }
    }

    private sealed class MultiFormatter : ICborFormatter<int>, ICborFormatter<long>
    {
        void ICborFormatter<int>.Serialize<W>(ref W buffer, ref CborSerializationContext context, int value)
            => throw new NotSupportedException();
        int ICborFormatter<int>.Deserialize<R>(ref R buffer, ref CborDeserializationContext context)
            => throw new NotSupportedException();
        void ICborFormatter<long>.Serialize<W>(ref W buffer, ref CborSerializationContext context, long value)
            => throw new NotSupportedException();
        long ICborFormatter<long>.Deserialize<R>(ref R buffer, ref CborDeserializationContext context)
            => throw new NotSupportedException();
    }

    private sealed class UnusedFormatter<T> : ICborFormatter<T>
    {
        public static readonly UnusedFormatter<T> Instance = new();
        public void Serialize<W>(ref W buffer, ref CborSerializationContext context, T value)
            where W : struct, IWriteBuffer
#if NET9_0_OR_GREATER
                , allows ref struct
#endif
            => throw new NotSupportedException();
        public T Deserialize<R>(ref R buffer, ref CborDeserializationContext context)
            where R : struct, IReadBuffer
#if NET9_0_OR_GREATER
                , allows ref struct
#endif
            => throw new NotSupportedException();
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
        private readonly OffsetFormatter formatter = new();
        public bool UseOffset { get; set; }
        public int IntegerResolutions { get; private set; }
        public override ICborFormatter<T>? GetFormatter<T>()
        {
            if (typeof(T) == typeof(int))
            {
                IntegerResolutions++;
                if (UseOffset)
                {
                    return (ICborFormatter<T>)(object)formatter;
                }
            }

            return TestResolver.Instance.GetFormatter<T>();
        }
    }

    private sealed class OffsetFormatter : ICborFormatter<int>
    {
        public void Serialize<W>(ref W buffer, ref CborSerializationContext context, int value)
            where W : struct, IWriteBuffer
#if NET9_0_OR_GREATER
                , allows ref struct
#endif
            => buffer.WriteInt64(value + 10);
        public int Deserialize<R>(ref R buffer, ref CborDeserializationContext context)
            where R : struct, IReadBuffer
#if NET9_0_OR_GREATER
                , allows ref struct
#endif
            => checked((int)buffer.ReadInt64() - 10);
    }
}
