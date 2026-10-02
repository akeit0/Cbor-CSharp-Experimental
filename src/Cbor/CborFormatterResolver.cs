using System.Runtime.CompilerServices;
using System.Threading;
using Cbor.Internal;
using SerializerFoundation;

namespace Cbor;

/// <summary>Owns buffer-specific formatter graphs, initialized once and published atomically.</summary>
public sealed class CborFormatterResolver
{
    private const int InitialTableCapacity = 16;
    private const int TableGrowthFactor = 2;
    private readonly CborFormatterFactory factory;
    private readonly object gate = new();
    private object?[] table = [];
    private readonly Dictionary<int, object> constructing = new();
    private readonly HashSet<int> creating = new();
    private Exception? constructionFailure;
    private static int nextId;

    /// <summary>Creates an independent cache over the factory's uninitialized formatter graphs.</summary>
    public CborFormatterResolver(CborFormatterFactory factory)
    {
        this.factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

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
            if (constructing.TryGetValue(id, out var cycle))
            {
                return (ICborFormatter<TWriteBuffer, TReadBuffer, T>)cycle;
            }
            if (!creating.Add(id)) { throw new InvalidOperationException("A factory recursively requested a formatter before returning its instance."); }
            try
            {
                object? created = factory.CreateFormatter<TWriteBuffer, TReadBuffer>(typeof(T));
                var result = created is null ? new MissingFormatter<TWriteBuffer, TReadBuffer, T>() :
                    created as ICborFormatter<TWriteBuffer, TReadBuffer, T> ??
                    throw new InvalidOperationException("The CBOR factory returned a formatter for a different buffer pair or value type.");
                constructing.Add(id, result);
                result.Initialize(this);
                if (root)
                {
                    if (constructionFailure is { } failure)
                    {
                        throw new InvalidOperationException("A formatter dependency failed initialization; the entire graph was abandoned.", failure);
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
                if (root) { constructing.Clear(); creating.Clear(); constructionFailure = null; }
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
