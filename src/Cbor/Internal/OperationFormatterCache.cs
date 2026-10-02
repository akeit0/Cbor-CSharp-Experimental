using System.Buffers;
using System.Runtime.CompilerServices;

namespace Cbor.Internal;

/// <summary>Lazy operation-local resolution. No formatter dependencies are traversed during initialization.</summary>
internal struct OperationFormatterCache
{
    private const int InlineCapacity = 2;
    private const int InitialTableCapacity = 16;
    private const int GrowthFactor = 2;
    private const int LoadFactorDivisor = 2;
    private InlineEntry first;
    private InlineEntry second;
    // Logical capacity is a power of two even when ArrayPool returns a larger array.
    // At most half the slots are occupied, so every miss reaches an empty slot.
    private Entry[]? table;
    private int capacity;
    private int count;

    // Hits first verify the exact CLR type. Entries can only be published from a matching
    // ICborFormatter<T>, so the reference reinterpretation avoids a redundant interface cast
    // (particularly costly for shared reference-type generics on compatibility runtimes).
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ICborFormatter<T> GetRequiredFormatter<T>(CborFormatterResolver resolver)
    {
        Type type = typeof(T);
        if (first.Type == type) { return Unsafe.As<ICborFormatter<T>>(first.Formatter)!; }
        if (second.Type == type) { return Unsafe.As<ICborFormatter<T>>(second.Formatter)!; }
        if (first.Type is null)
        {
            var formatter = resolver.GetRequiredFormatter<T>();
            if (count != 0)
            {
                return PublishReentrant(formatter);
            }

            first = new InlineEntry { Type = type, Formatter = formatter };
            count = 1;
            return formatter;
        }

        if (second.Type is null)
        {
            var formatter = resolver.GetRequiredFormatter<T>();
            if (count != 1)
            {
                return PublishReentrant(formatter);
            }

            second = new InlineEntry { Type = type, Formatter = formatter };
            count = InlineCapacity;
            return formatter;
        }

        if (table is not null)
        {
            int index = TypeHash<T>.Value & (capacity - 1);
            while (table[index].Type is not null)
            {
                if (table[index].Type == type)
                {
                    return Unsafe.As<ICborFormatter<T>>(table[index].Formatter)!;
                }

                index = (index + 1) & (capacity - 1);
            }
        }

        return ResolveCold<T>(resolver);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private ICborFormatter<T> ResolveCold<T>(CborFormatterResolver resolver)
    {
        // Resolve before publishing: throwing or missing resolutions never leave an incomplete entry.
        int previousCount = count;
        var formatter = resolver.GetRequiredFormatter<T>();
        if (count != previousCount)
        {
            return PublishReentrant(formatter);
        }

        AddResolved(formatter);
        return formatter;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private ICborFormatter<T> PublishReentrant<T>(ICborFormatter<T> formatter)
    {
        // Custom resolver callbacks may resolve other types, grow the table, or even finish
        // resolving T before the outer call returns. Preserve the first completed selection.
        Type type = typeof(T);
        if (first.Type == type) { return Unsafe.As<ICborFormatter<T>>(first.Formatter)!; }
        if (second.Type == type) { return Unsafe.As<ICborFormatter<T>>(second.Formatter)!; }
        if (first.Type is null)
        {
            first = new InlineEntry { Type = type, Formatter = formatter };
            count = 1;
        }
        else if (second.Type is null)
        {
            second = new InlineEntry { Type = type, Formatter = formatter };
            count = InlineCapacity;
        }
        else
        {
            if (table is not null)
            {
                int index = TypeHash<T>.Value & (capacity - 1);
                while (table[index].Type is not null)
                {
                    if (table[index].Type == type)
                    {
                        return Unsafe.As<ICborFormatter<T>>(table[index].Formatter)!;
                    }

                    index = (index + 1) & (capacity - 1);
                }
            }

            AddResolved(formatter);
        }

        return formatter;
    }

    private void AddResolved<T>(ICborFormatter<T> formatter)
    {
        if (table is null)
        {
            table = ArrayPool<Entry>.Shared.Rent(InitialTableCapacity);
            capacity = InitialTableCapacity;
        }
        else if (count - InlineCapacity == capacity / LoadFactorDivisor)
        {
            Grow();
        }

        Insert(table, capacity, new Entry { Type = typeof(T), Formatter = formatter, Hash = TypeHash<T>.Value });
        count++;
    }

    private void Grow()
    {
        int nextCapacity = checked(capacity * GrowthFactor);
        var next = ArrayPool<Entry>.Shared.Rent(nextCapacity);
        var previous = table!;
        for (int i = 0; i < capacity; i++)
        {
            if (previous[i].Type is not null)
            {
                Insert(next, nextCapacity, previous[i]);
            }
        }

        table = next;
        capacity = nextCapacity;
        ArrayPool<Entry>.Shared.Return(previous, clearArray: true);
    }

    private static void Insert(Entry[] entries, int length, Entry entry)
    {
        int index = entry.Hash & (length - 1);
        while (entries[index].Type is not null)
        {
            index = (index + 1) & (length - 1);
        }

        entries[index] = entry;
    }

    internal void Dispose()
    {
        var rented = table;
        this = default;
        if (rented is not null)
        {
            // Neither custom resolvers, formatters nor their model types survive in the shared pool.
            ArrayPool<Entry>.Shared.Return(rented, clearArray: true);
        }
    }

    private static class TypeHash<T>
    {
        // Metadata only: no resolver or formatter initialization, including for recursive models.
        internal static readonly int Value = typeof(T).GetHashCode();
    }

    private struct InlineEntry
    {
        internal Type? Type;
        internal object? Formatter;
    }

    private struct Entry
    {
        internal Type? Type;
        internal object? Formatter;
        internal int Hash;
    }
}
