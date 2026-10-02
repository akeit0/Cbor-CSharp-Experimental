using System.Runtime.CompilerServices;
using System.Threading;
using Cbor.Internal;
using SerializerFoundation;

namespace Cbor;

/// <summary>Owns buffer-specific formatter graphs, initialized once and published atomically.</summary>
/// <remarks>Use factories for new providers. The single-type GetFormatter overload remains a compatible-buffer adapter for existing providers.</remarks>
public class CborFormatterResolver
{
    private const int InitialTableCapacity = 16;
    private const int TableGrowthFactor = 2;
    private readonly CborFormatterFactory? configuredFactory;
    private readonly object gate = new();
    private object?[] table = [];
    private object?[] compatible = [];
    private readonly Dictionary<int, object> constructing = new();
    private readonly HashSet<int> creating = new();
    private bool foundCompatible;
    private Exception? constructionFailure;
    private static int nextId;

    /// <summary>Creates an independent cache over the factory's uninitialized formatter graphs.</summary>
    public CborFormatterResolver(CborFormatterFactory factory)
    {
        configuredFactory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    /// <summary>Constructs a derived generated or legacy provider.</summary>
    protected CborFormatterResolver() { }

    /// <summary>Factory supplying uninitialized instances; null enables a legacy single-type provider.</summary>
    protected virtual CborFormatterFactory? Provider => configuredFactory;

    internal bool HasFactoryProvider => Provider is not null;

    /// <summary>Returns an ordinary-buffer adapter, or null for an unsupported type. Override only for legacy providers.</summary>
    public virtual ICborFormatter<T>? GetFormatter<T>() => CreateProvidedFormatter(
        typeof(CompatibleArrayPoolListWriteBuffer), typeof(CompatibleReadOnlySpanReadBuffer), typeof(T)) is null
        ? null : new FactoryFormatter<T>(this);

    /// <summary>Returns a legacy adapter or throws when the type is unsupported.</summary>
    public ICborFormatter<T> GetRequiredFormatter<T>() => GetFormatter<T>() ??
        throw new NotSupportedException("No CBOR formatter is registered for " + typeof(T).FullName + ".");

    /// <summary>Gets a formatter for the exact buffer pair, constructing and initializing its dependencies once.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ICborFormatter<TWriteBuffer, TReadBuffer, T> GetFormatter<TWriteBuffer, TReadBuffer, T>()
        where TWriteBuffer : struct, IWriteBuffer
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
        where TReadBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
    {
        int id = TypeId<TWriteBuffer, TReadBuffer, T>.Value;
        var snapshot = Volatile.Read(ref table);
        if ((uint)id < (uint)snapshot.Length && snapshot[id] is { } value)
        {
            return Unsafe.As<ICborFormatter<TWriteBuffer, TReadBuffer, T>>(value);
        }
        return CreateGraph<TWriteBuffer, TReadBuffer, T>(id);
    }

    internal bool TryGetFormatter<TWriteBuffer, TReadBuffer, T>(out ICborFormatter<TWriteBuffer, TReadBuffer, T> formatter)
        where TWriteBuffer : struct, IWriteBuffer
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
        where TReadBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
    {
        int id = TypeId<TWriteBuffer, TReadBuffer, T>.Value;
        var snapshot = Volatile.Read(ref table);
        if ((uint)id < (uint)snapshot.Length && snapshot[id] is { } cached)
        {
            formatter = Unsafe.As<ICborFormatter<TWriteBuffer, TReadBuffer, T>>(cached);
            return true;
        }
        var compat = Volatile.Read(ref compatible);
        if ((uint)id < (uint)compat.Length && compat[id] is not null)
        {
            formatter = null!;
            return false;
        }
        formatter = GetFormatter<TWriteBuffer, TReadBuffer, T>();
        return formatter is not ICompatiblePairRequired;
    }

    internal object? CreateProvidedFormatter(Type writeBufferType, Type readBufferType, Type valueType)
        => Provider?.CreateFormatter(writeBufferType, readBufferType, valueType);
#if NET9_0_OR_GREATER
    internal object? CreateProvidedFormatter<TWriteBuffer, TReadBuffer>(Type valueType)
        where TWriteBuffer : struct, IWriteBuffer, allows ref struct
        where TReadBuffer : struct, IReadBuffer, allows ref struct
        => Provider?.CreateFormatter<TWriteBuffer, TReadBuffer>(valueType);
#endif

    internal virtual object? CreateRaw<TWriteBuffer, TReadBuffer, T>()
        where TWriteBuffer : struct, IWriteBuffer
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
        where TReadBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
    {
        if (Provider is { } provider)
        {
#if NET9_0_OR_GREATER
            var created = provider.CreateFormatter<TWriteBuffer, TReadBuffer>(typeof(T));
            if (created is null && (typeof(TWriteBuffer).IsByRefLike || typeof(TReadBuffer).IsByRefLike) &&
                provider.CreateFormatter(typeof(CompatibleArrayPoolListWriteBuffer), typeof(CompatibleReadOnlySpanReadBuffer), typeof(T)) is not null)
            {
                return CompatiblePairRequiredProvider.Instance;
            }
            return created;
#else
            return provider.CreateFormatter(typeof(TWriteBuffer), typeof(TReadBuffer), typeof(T));
#endif
        }
        var legacy = GetFormatter<T>();
        if (legacy is IFactoryFormatter bridge)
        {
#if NET9_0_OR_GREATER
            return bridge.Resolver.CreateProvidedFormatter<TWriteBuffer, TReadBuffer>(typeof(T));
#else
            return bridge.Resolver.CreateProvidedFormatter(typeof(TWriteBuffer), typeof(TReadBuffer), typeof(T));
#endif
        }
        if (legacy is null) { return null; }
        return LegacyFormatterAdapter.Create<TWriteBuffer, TReadBuffer, T>(legacy) ?? CompatiblePairRequiredProvider.Instance;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private ICborFormatter<TWriteBuffer, TReadBuffer, T> CreateGraph<TWriteBuffer, TReadBuffer, T>(int id)
        where TWriteBuffer : struct, IWriteBuffer
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
        where TReadBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
    {
        lock (gate)
        {
            bool root = constructing.Count == 0 && creating.Count == 0;
            var snapshot = table;
            if ((uint)id < (uint)snapshot.Length && snapshot[id] is { } cached)
            {
                return (ICborFormatter<TWriteBuffer, TReadBuffer, T>)cached;
            }
            var compat = compatible;
            if ((uint)id < (uint)compat.Length && compat[id] is { } sentinel)
            {
                if (!root) { foundCompatible = true; }
                return (ICborFormatter<TWriteBuffer, TReadBuffer, T>)sentinel;
            }
            if (constructing.TryGetValue(id, out var cycle))
            {
                return (ICborFormatter<TWriteBuffer, TReadBuffer, T>)cycle;
            }
            if (!creating.Add(id)) { throw new InvalidOperationException("A factory recursively requested a formatter before returning its instance."); }
            try
            {
                object? created = CreateRaw<TWriteBuffer, TReadBuffer, T>();
                ICborFormatter<TWriteBuffer, TReadBuffer, T> result;
                if (created is CompatiblePairRequiredProvider)
                {
                    foundCompatible = true;
                    result = new MissingFormatter<TWriteBuffer, TReadBuffer, T>();
                }
                else if (created is null)
                {
#if NET9_0_OR_GREATER
                    if ((typeof(TWriteBuffer).IsByRefLike || typeof(TReadBuffer).IsByRefLike) &&
                        CreateRaw<CompatibleArrayPoolListWriteBuffer, CompatibleReadOnlySpanReadBuffer, T>() is not null)
                    {
                        foundCompatible = true;
                    }
#endif
                    result = new MissingFormatter<TWriteBuffer, TReadBuffer, T>();
                }
                else
                {
                    result = created as ICborFormatter<TWriteBuffer, TReadBuffer, T> ??
                        throw new InvalidOperationException("The CBOR factory returned a formatter for a different buffer pair or value type.");
                }
                constructing.Add(id, result);
                result.Initialize(this);
                if (root)
                {
                    if (constructionFailure is { } failure)
                    {
                        throw new InvalidOperationException("A formatter dependency failed initialization; the entire graph was abandoned.", failure);
                    }
                    if (foundCompatible)
                    {
                        var marker = new CompatiblePairRequired<TWriteBuffer, TReadBuffer, T>();
                        Publish(ref compatible, new Dictionary<int, object> { [id] = marker });
                        return marker;
                    }
                    Publish(ref table, constructing);
                }
                return result;
            }
            catch (Exception exception)
            {
                constructionFailure ??= exception;
                throw;
            }
            finally
            {
                creating.Remove(id);
                if (root) { constructing.Clear(); creating.Clear(); foundCompatible = false; constructionFailure = null; }
            }
        }
    }

    private static void Publish(ref object?[] destination, Dictionary<int, object> additions)
    {
        int length = destination.Length;
        foreach (int id in additions.Keys) { length = Math.Max(length, checked(id + 1)); }
        int capacity = length > destination.Length ? Math.Max(length, Math.Max(InitialTableCapacity, destination.Length * TableGrowthFactor)) : destination.Length;
        var snapshot = new object?[capacity];
        Array.Copy(destination, snapshot, destination.Length);
        foreach (var entry in additions) { snapshot[entry.Key] = entry.Value; }
        Volatile.Write(ref destination, snapshot);
    }

    private static class TypeId<TWriteBuffer, TReadBuffer, T>
    where TWriteBuffer : struct, IWriteBuffer
#if NET9_0_OR_GREATER
    , allows ref struct
#endif
    where TReadBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
    , allows ref struct
#endif
    {
        internal static readonly int Value = Interlocked.Increment(ref nextId) - 1;
    }
}

/// <summary>An immutable provider chain. Child formatter graphs are always initialized against the complete chain.</summary>
public sealed class CborCompositeResolver : CborFormatterResolver
{
    private readonly CborFormatterResolver[] resolvers;
    /// <summary>Copies the provider sequence; the first provider serving a type wins.</summary>
    public CborCompositeResolver(params CborFormatterResolver[] resolvers)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(resolvers);
#else
        if (resolvers is null) { throw new ArgumentNullException(nameof(resolvers)); }
#endif
        this.resolvers = (CborFormatterResolver[])resolvers.Clone();
        if (Array.Exists(this.resolvers, static resolver => resolver is null))
        {
            throw new ArgumentException("Resolvers cannot contain null.", nameof(resolvers));
        }
    }
    /// <inheritdoc />
    public override ICborFormatter<T>? GetFormatter<T>()
    {
        foreach (var resolver in resolvers)
        {
            var formatter = resolver.GetFormatter<T>();
            if (formatter is not null) { return formatter; }
        }
        return null;
    }
    internal override object? CreateRaw<TWriteBuffer, TReadBuffer, T>()
    {
        foreach (var resolver in resolvers)
        {
            var formatter = resolver.CreateRaw<TWriteBuffer, TReadBuffer, T>();
            if (formatter is not null) { return formatter; }
        }
        return null;
    }
}

/// <summary>Builds an explicit type registry. Build takes an immutable snapshot.</summary>
public sealed class CborFormatterRegistry
{
    private readonly Dictionary<Type, object> formatters = new();

    /// <summary>Registers a thread-safe formatter. Duplicate registrations are rejected.</summary>
    public CborFormatterRegistry Add<T>(ICborFormatter<T> formatter)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(formatter);
#else
        if (formatter is null)
        {
            throw new ArgumentNullException(nameof(formatter));
        }
#endif

        if (formatters.ContainsKey(typeof(T)))
        {
            throw new ArgumentException("A formatter is already registered for " + typeof(T).FullName + ".", nameof(formatter));
        }

        formatters.Add(typeof(T), formatter);
        return this;
    }

    /// <summary>Freezes the current registrations; the builder can then be reused independently.</summary>
    public CborFormatterResolver Build() => new RegistryResolver(new Dictionary<Type, object>(formatters));

    private sealed class RegistryResolver(Dictionary<Type, object> formatters) : CborFormatterResolver
    {
        public override ICborFormatter<T>? GetFormatter<T>() =>
            formatters.TryGetValue(typeof(T), out var formatter) ? (ICborFormatter<T>)formatter : null;
    }
}
