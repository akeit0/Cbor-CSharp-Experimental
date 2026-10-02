using SerializerFoundation;
#if NET8_0_OR_GREATER
using System.Runtime.InteropServices;
#endif

namespace Cbor;

/// <summary>Formats nullable value types, preserving CBOR null distinctly from undefined.</summary>
/// <typeparam name="TWriteBuffer">Write buffer type.</typeparam>
/// <typeparam name="TReadBuffer">Read buffer type.</typeparam>
/// <typeparam name="T">Underlying value type.</typeparam>
public sealed class CborNullableFormatter<TWriteBuffer, TReadBuffer, T> :
    ICborFormatter<TWriteBuffer, TReadBuffer, T?>
    where TWriteBuffer : struct, IWriteBuffer
#if NET9_0_OR_GREATER
    , allows ref struct
#endif
    where TReadBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
    , allows ref struct
#endif
    where T : struct
{
    /// <inheritdoc />
    private ICborFormatter<TWriteBuffer, TReadBuffer, T> formatter = null!;
    /// <inheritdoc />
    public void Initialize(CborFormatterResolver resolver)
    {
        formatter = resolver.GetFormatter<TWriteBuffer, TReadBuffer, T>();
    }
    /// <inheritdoc />
    public void Serialize(ref TWriteBuffer buffer, ref CborSerializationContext context, T? value)
    {
        if (value.HasValue)
        {
            // The nullable wrapper is the same wire item, so it does not charge a second item.
            context.SerializeSameItem(ref buffer, value.Value, formatter);
        }
        else
        {
            buffer.WriteNull();
        }
    }

    /// <inheritdoc />
    public T? Deserialize(ref TReadBuffer buffer, ref CborDeserializationContext context)
        => CborDeserializationContext.TryReadNull(ref buffer) ? null :
            context.DeserializeSameItem(ref buffer, formatter);
}

/// <summary>Formats CLR arrays as CBOR arrays. Byte arrays use the built-in byte-string formatter.</summary>
/// <typeparam name="TWriteBuffer">Write buffer type.</typeparam>
/// <typeparam name="TReadBuffer">Read buffer type.</typeparam>
/// <typeparam name="T">Element type.</typeparam>
public sealed class CborArrayFormatter<TWriteBuffer, TReadBuffer, T> :
    ICborFormatter<TWriteBuffer, TReadBuffer, T[]?>
    where TWriteBuffer : struct, IWriteBuffer
#if NET9_0_OR_GREATER
    , allows ref struct
#endif
    where TReadBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
    , allows ref struct
#endif
{
    /// <inheritdoc />
    private ICborFormatter<TWriteBuffer, TReadBuffer, T> formatter = null!;
    /// <inheritdoc />
    public void Initialize(CborFormatterResolver resolver)
    {
        formatter = resolver.GetFormatter<TWriteBuffer, TReadBuffer, T>();
    }
    /// <inheritdoc />
    public void Serialize(ref TWriteBuffer buffer, ref CborSerializationContext context, T[]? value)
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
    public T[]? Deserialize(ref TReadBuffer buffer, ref CborDeserializationContext context)
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
            var items = new List<T>();
            while (!buffer.TryReadBreak())
            {
                context.CheckCollectionLength(items.Count + 1);
                items.Add(context.Deserialize(ref buffer, formatter));
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
/// <typeparam name="TWriteBuffer">Write buffer type.</typeparam>
/// <typeparam name="TReadBuffer">Read buffer type.</typeparam>
/// <typeparam name="T">Element type.</typeparam>
public sealed class CborListFormatter<TWriteBuffer, TReadBuffer, T> :
    ICborFormatter<TWriteBuffer, TReadBuffer, List<T>?>
    where TWriteBuffer : struct, IWriteBuffer
#if NET9_0_OR_GREATER
    , allows ref struct
#endif
    where TReadBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
    , allows ref struct
#endif
{
    /// <inheritdoc />
    private ICborFormatter<TWriteBuffer, TReadBuffer, T> formatter = null!;
    /// <inheritdoc />
    public void Initialize(CborFormatterResolver resolver)
    {
        formatter = resolver.GetFormatter<TWriteBuffer, TReadBuffer, T>();
    }
    /// <inheritdoc />
    public void Serialize(ref TWriteBuffer buffer, ref CborSerializationContext context, List<T>? value)
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
    public List<T>? Deserialize(ref TReadBuffer buffer, ref CborDeserializationContext context)
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
/// <typeparam name="TWriteBuffer">Write buffer type.</typeparam>
/// <typeparam name="TReadBuffer">Read buffer type.</typeparam>
/// <typeparam name="TKey">Non-null key type. Its comparer must resist adversarial hash collisions.</typeparam>
/// <typeparam name="TValue">Value type.</typeparam>
public sealed class CborDictionaryFormatter<TWriteBuffer, TReadBuffer, TKey, TValue> :
    ICborFormatter<TWriteBuffer, TReadBuffer, Dictionary<TKey, TValue>?>
    where TWriteBuffer : struct, IWriteBuffer
#if NET9_0_OR_GREATER
    , allows ref struct
#endif
    where TReadBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
    , allows ref struct
#endif
    where TKey : notnull
{
    /// <inheritdoc />
    private ICborFormatter<TWriteBuffer, TReadBuffer, TKey> keyFormatter = null!;
    private ICborFormatter<TWriteBuffer, TReadBuffer, TValue> valueFormatter = null!;
    /// <inheritdoc />
    public void Initialize(CborFormatterResolver resolver)
    {
        keyFormatter = resolver.GetFormatter<TWriteBuffer, TReadBuffer, TKey>();
        valueFormatter = resolver.GetFormatter<TWriteBuffer, TReadBuffer, TValue>();
    }
    private readonly IEqualityComparer<TKey> comparer;

    /// <summary>Creates a formatter with the supplied comparer or a process-keyed built-in comparer.</summary>
    public CborDictionaryFormatter(IEqualityComparer<TKey>? comparer = null)
    {
        this.comparer = comparer ?? CborKeyComparers.Get<TKey>();
    }

    /// <inheritdoc />
    public void Serialize(ref TWriteBuffer buffer, ref CborSerializationContext context, Dictionary<TKey, TValue>? value)
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
    public Dictionary<TKey, TValue>? Deserialize(ref TReadBuffer buffer, ref CborDeserializationContext context)
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

#if NET8_0_OR_GREATER
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
