using System.Runtime.CompilerServices;
using Cbor.Internal;
using SerializerFoundation;

namespace Cbor;

/// <summary>Shared write limits and formatter resolution for one operation.</summary>
/// <remarks>Single-owner mutable state: pass by ref, never copy or box, and dispose in finally.</remarks>
public struct CborSerializationContext : IDisposable
{
    private readonly CborSerializerOptions options;
    private readonly bool builtinResolution;
    private OperationFormatterCache formatters;
    private int depth;
    private long items;
    private readonly long start;

    /// <summary>Creates context at the buffer's initial byte position.</summary>
    public CborSerializationContext(CborSerializerOptions options, long bytesWritten = 0)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        builtinResolution = options.Resolver is CborBuiltinResolver;
        start = bytesWritten;
        formatters = default;
        depth = 0;
        items = 0;
    }

    /// <summary>Resolves each type lazily and shares its formatter throughout this operation.</summary>
    /// <remarks>Resolver changes take effect on the next operation. Formatters must be stateless and thread-safe.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ICborFormatter<T> GetRequiredFormatter<T>() => builtinResolution
        ? CborBuiltinResolver.Instance.GetRequiredFormatter<T>()
        : formatters.GetRequiredFormatter<T>(options.Resolver);

    /// <summary>Releases pooled formatter storage. Dispose once after completing or abandoning the operation.</summary>
    public void Dispose() => formatters.Dispose();

    /// <summary>Immutable options for this operation.</summary>
    public readonly CborSerializerOptions Options => options;

    /// <summary>Serializes a nested or root value and charges its item and byte budget.</summary>
    public void Serialize<TWriteBuffer, T>(ref TWriteBuffer buffer, T value)
        where TWriteBuffer : struct, IWriteBuffer
#if NET9_0_OR_GREATER
            , allows ref struct
#endif
    {
        ChargeItem();

        SerializeCore(ref buffer, value, GetRequiredFormatter<T>());
    }

    /// <summary>Serializes with a formatter resolved from this operation's resolver, retaining shared budgets.</summary>
    /// <remarks>Generated and collection formatters use this overload to reuse a resolved child formatter.</remarks>
    public void Serialize<TWriteBuffer, T>(ref TWriteBuffer buffer, T value, ICborFormatter<T> formatter)
        where TWriteBuffer : struct, IWriteBuffer
#if NET9_0_OR_GREATER
            , allows ref struct
#endif
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(formatter);
#else
        if (formatter is null)
        {
            throw new ArgumentNullException(nameof(formatter));
        }
#endif

        ChargeItem();
        SerializeCore(ref buffer, value, formatter);
    }

    private void SerializeCore<TWriteBuffer, T>(ref TWriteBuffer buffer, T value, ICborFormatter<T> formatter)
        where TWriteBuffer : struct, IWriteBuffer
#if NET9_0_OR_GREATER
            , allows ref struct
#endif
    {
        formatter.Serialize(ref buffer, ref this, value);
        CheckEncodedLength(buffer.BytesWritten);
    }

    /// <summary>Writes an object contract's integer key, independently of CLR integer formatter overrides.</summary>
    public void WriteObjectKey<TWriteBuffer>(ref TWriteBuffer buffer, int key)
        where TWriteBuffer : struct, IWriteBuffer
#if NET9_0_OR_GREATER
            , allows ref struct
#endif
    {
#if NET8_0_OR_GREATER
        ArgumentOutOfRangeException.ThrowIfNegative(key);
#else
        if (key < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(key));
        }
#endif

        ChargeItem();
        CheckEncodedLength(buffer.BytesWritten, CborPrimitives.GetHeaderLength((ulong)key));
        buffer.WriteUInt64((ulong)key);
    }

    private void ChargeItem()
    {
        if (items >= options.ReaderOptions.MaxItems)
        {
            throw new InvalidDataException("The CBOR item budget was exceeded.");
        }

        items++;
    }

    /// <summary>Enters an array, map, tag, or indefinite string before descending.</summary>
    public void EnterContainer()
    {
        if (depth == options.ReaderOptions.MaxDepth)
        {
            throw new InvalidDataException("The CBOR nesting depth was exceeded.");
        }

        depth++;
    }

    /// <summary>Leaves a container; call from finally.</summary>
    public void ExitContainer() => depth--;

    /// <summary>Checks a collection's size before writing its header.</summary>
    public readonly void CheckCollectionLength(int count)
    {
        if ((uint)count > (uint)options.MaxCollectionLength)
        {
            throw new InvalidDataException("The CBOR collection length limit was exceeded.");
        }
    }

    /// <summary>Checks string byte length before writing its header or requesting memory.</summary>
    public readonly void CheckStringLength(int bytes)
    {
        if ((uint)bytes > (uint)options.MaxStringLength || bytes > options.ReaderOptions.MaxEncodedLength)
        {
            throw new InvalidDataException("The CBOR string length limit was exceeded.");
        }
    }

    /// <summary>Checks the remaining encoded byte budget before requesting memory for a known-length payload.</summary>
    /// <param name="bytesWritten">The borrowed buffer's current absolute BytesWritten position.</param>
    /// <param name="additionalLength">Bytes about to be written, including their headers.</param>
    public readonly void CheckEncodedLength(long bytesWritten, long additionalLength = 0)
    {
#if NET8_0_OR_GREATER
        ArgumentOutOfRangeException.ThrowIfNegative(additionalLength);
#else
        if (additionalLength < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(additionalLength));
        }
#endif

        long used = bytesWritten - start;
        long limit = options.ReaderOptions.MaxEncodedLength;
        if (used < 0 || used > limit || additionalLength > limit - used)
        {
            throw new InvalidDataException("The CBOR encoded byte budget was exceeded.");
        }
    }
}
