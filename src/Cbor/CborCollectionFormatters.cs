using SerializerFoundation;
#if NET9_0_OR_GREATER
using System.Runtime.InteropServices;
#endif

namespace Cbor;

/// <summary>Formats nullable value types, preserving CBOR null distinctly from undefined.</summary>
/// <typeparam name="T">Underlying value type.</typeparam>
public sealed class CborNullableFormatter<T> : ICborFormatter<T?> where T : struct
{
    /// <inheritdoc />
    public void Serialize<TWriteBuffer>(ref TWriteBuffer buffer, ref CborSerializationContext context, T? value)
        where TWriteBuffer : struct, IWriteBuffer
#if NET9_0_OR_GREATER
            , allows ref struct
#endif
    {
        if (value.HasValue)
        {
            // The nullable wrapper is the same wire item, so it does not charge a second item.
            context.Options.Resolver.GetRequiredFormatter<T>().Serialize(ref buffer, ref context, value.Value);
        }
        else
        {
            buffer.WriteNull();
        }
    }

    /// <inheritdoc />
    public T? Deserialize<TReadBuffer>(ref TReadBuffer buffer, ref CborDeserializationContext context)
        where TReadBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
            , allows ref struct
#endif
        => CborDeserializationContext.TryReadNull(ref buffer) ? null :
            context.Options.Resolver.GetRequiredFormatter<T>().Deserialize(ref buffer, ref context);
}

/// <summary>Formats CLR arrays as CBOR arrays. Byte arrays use the built-in byte-string formatter.</summary>
/// <typeparam name="T">Element type.</typeparam>
public sealed class CborArrayFormatter<T> : ICborFormatter<T[]?>
{
    /// <inheritdoc />
    public void Serialize<TWriteBuffer>(ref TWriteBuffer buffer, ref CborSerializationContext context, T[]? value)
        where TWriteBuffer : struct, IWriteBuffer
#if NET9_0_OR_GREATER
            , allows ref struct
#endif
    {
        if (value is null)
        {
            buffer.WriteNull();
            return;
        }

        context.CheckCollectionLength(value.Length);
        context.EnterContainer();
        try
        {
            buffer.WriteArrayHeader((ulong)value.Length);
            var formatter = value.Length == 0 ? null : context.Options.Resolver.GetRequiredFormatter<T>();
            foreach (T item in value)
            {
                context.Serialize(ref buffer, item, formatter!);
            }
        }
        finally
        {
            context.ExitContainer();
        }
    }

    /// <inheritdoc />
    public T[]? Deserialize<TReadBuffer>(ref TReadBuffer buffer, ref CborDeserializationContext context)
        where TReadBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
            , allows ref struct
#endif
    {
        if (CborDeserializationContext.TryReadNull(ref buffer))
        {
            return null;
        }

        context.EnterContainer();
        try
        {
            int? length = context.ReadArrayLength(ref buffer);
            if (length.HasValue)
            {
                if (length.Value == 0)
                {
                    return Array.Empty<T>();
                }

                var formatter = context.Options.Resolver.GetRequiredFormatter<T>();
                var result = new T[length.Value];
                for (int i = 0; i < result.Length; i++)
                {
                    result[i] = context.Deserialize(ref buffer, formatter);
                }

                return result;
            }

            if (buffer.TryReadBreak())
            {
                return Array.Empty<T>();
            }

            var elementFormatter = context.Options.Resolver.GetRequiredFormatter<T>();
            var items = new List<T>();
            while (!buffer.TryReadBreak())
            {
                context.CheckCollectionLength(items.Count + 1);
                items.Add(context.Deserialize(ref buffer, elementFormatter));
            }

            return items.ToArray();
        }
        finally
        {
            context.ExitContainer();
        }
    }
}

/// <summary>Formats lists as CBOR arrays, accepting definite and indefinite encodings.</summary>
/// <typeparam name="T">Element type.</typeparam>
public sealed class CborListFormatter<T> : ICborFormatter<List<T>?>
{
    /// <inheritdoc />
    public void Serialize<TWriteBuffer>(ref TWriteBuffer buffer, ref CborSerializationContext context, List<T>? value)
        where TWriteBuffer : struct, IWriteBuffer
#if NET9_0_OR_GREATER
            , allows ref struct
#endif
    {
        if (value is null)
        {
            buffer.WriteNull();
            return;
        }

        context.CheckCollectionLength(value.Count);
        context.EnterContainer();
        try
        {
            buffer.WriteArrayHeader((ulong)value.Count);
            var formatter = value.Count == 0 ? null : context.Options.Resolver.GetRequiredFormatter<T>();
            foreach (T item in value)
            {
                context.Serialize(ref buffer, item, formatter!);
            }
        }
        finally
        {
            context.ExitContainer();
        }
    }

    /// <inheritdoc />
    public List<T>? Deserialize<TReadBuffer>(ref TReadBuffer buffer, ref CborDeserializationContext context)
        where TReadBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
            , allows ref struct
#endif
    {
        if (CborDeserializationContext.TryReadNull(ref buffer))
        {
            return null;
        }

        context.EnterContainer();
        try
        {
            int? length = context.ReadArrayLength(ref buffer);
            var result = new List<T>(length ?? 0);
            if (length == 0 || (!length.HasValue && buffer.TryReadBreak()))
            {
                return result;
            }

            var formatter = context.Options.Resolver.GetRequiredFormatter<T>();
            for (int i = 0; !length.HasValue || i < length.Value; i++)
            {
                if (!length.HasValue && buffer.TryReadBreak())
                {
                    break;
                }

                context.CheckCollectionLength(i + 1);
                result.Add(context.Deserialize(ref buffer, formatter));
            }

            return result;
        }
        finally
        {
            context.ExitContainer();
        }
    }
}

/// <summary>Formats dictionaries as CBOR maps, rejecting duplicate CLR keys on read.</summary>
/// <remarks>Writes preserve dictionary enumeration order; this is not a deterministic map-order profile.</remarks>
/// <typeparam name="TKey">Non-null key type. Its comparer must resist adversarial hash collisions.</typeparam>
/// <typeparam name="TValue">Value type.</typeparam>
public sealed class CborDictionaryFormatter<TKey, TValue> : ICborFormatter<Dictionary<TKey, TValue>?> where TKey : notnull
{
    private readonly IEqualityComparer<TKey> comparer;

    /// <summary>Creates a formatter with the supplied comparer or a process-keyed built-in comparer.</summary>
    public CborDictionaryFormatter(IEqualityComparer<TKey>? comparer = null)
    {
        this.comparer = comparer ?? CborKeyComparers.Get<TKey>();
    }

    /// <inheritdoc />
    public void Serialize<TWriteBuffer>(ref TWriteBuffer buffer, ref CborSerializationContext context, Dictionary<TKey, TValue>? value)
        where TWriteBuffer : struct, IWriteBuffer
#if NET9_0_OR_GREATER
            , allows ref struct
#endif
    {
        if (value is null)
        {
            buffer.WriteNull();
            return;
        }

        context.CheckCollectionLength(value.Count);
        context.EnterContainer();
        try
        {
            buffer.WriteMapHeader((ulong)value.Count);
            var keyFormatter = value.Count == 0 ? null : context.Options.Resolver.GetRequiredFormatter<TKey>();
            var valueFormatter = value.Count == 0 ? null : context.Options.Resolver.GetRequiredFormatter<TValue>();
            foreach (var pair in value)
            {
                context.Serialize(ref buffer, pair.Key, keyFormatter!);
                context.Serialize(ref buffer, pair.Value, valueFormatter!);
            }
        }
        finally
        {
            context.ExitContainer();
        }
    }

    /// <inheritdoc />
    public Dictionary<TKey, TValue>? Deserialize<TReadBuffer>(ref TReadBuffer buffer, ref CborDeserializationContext context)
        where TReadBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
            , allows ref struct
#endif
    {
        if (CborDeserializationContext.TryReadNull(ref buffer))
        {
            return null;
        }

        context.EnterContainer();
        try
        {
            int? length = context.ReadMapLength(ref buffer);
            var result = new Dictionary<TKey, TValue>(length ?? 0, comparer);
            if (length == 0 || (!length.HasValue && buffer.TryReadBreak()))
            {
                return result;
            }

            var keyFormatter = context.Options.Resolver.GetRequiredFormatter<TKey>();
            var valueFormatter = context.Options.Resolver.GetRequiredFormatter<TValue>();
            for (int i = 0; !length.HasValue || i < length.Value; i++)
            {
                if (!length.HasValue && buffer.TryReadBreak())
                {
                    break;
                }

                context.CheckCollectionLength(i + 1);
                TKey key = context.Deserialize(ref buffer, keyFormatter);
                if (key is null)
                {
                    throw new InvalidDataException("A CBOR map contains a null or duplicate dictionary key.");
                }

#if NET9_0_OR_GREATER
                ref TValue? slot = ref CollectionsMarshal.GetValueRefOrAddDefault(result, key, out bool exists);
                if (exists)
                {
                    throw new InvalidDataException("A CBOR map contains a null or duplicate dictionary key.");
                }

                // The new dictionary is private until this method returns. Nested formatters cannot
                // mutate it, so its entry storage remains stable while the value is decoded.
                // On failure the entire dictionary is discarded with the operation context.
                slot = context.Deserialize(ref buffer, valueFormatter);
#else
                if (result.ContainsKey(key))
                {
                    throw new InvalidDataException("A CBOR map contains a null or duplicate dictionary key.");
                }

                TValue value = context.Deserialize(ref buffer, valueFormatter);
                result.Add(key, value);
#endif
            }

            return result;
        }
        finally
        {
            context.ExitContainer();
        }
    }
}
