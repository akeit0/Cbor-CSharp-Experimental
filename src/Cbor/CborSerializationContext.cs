using System.Runtime.CompilerServices;
using Cbor.Internal;
using SerializerFoundation;

namespace Cbor;

/// <summary>Shared write limits and formatter resolution for one operation.</summary>
/// <remarks>Single-owner mutable state: pass by ref, never copy or box, and dispose in finally.</remarks>
public struct CborSerializationContext : IDisposable
{
    private readonly CborSerializerOptions options;
    private int depth;
    private long items;
    private readonly long start;

    /// <summary>Creates context at the buffer's initial byte position.</summary>
    public CborSerializationContext(CborSerializerOptions options, long bytesWritten = 0)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        start = bytesWritten;
        depth = 0;
        items = 0;
    }

    /// <summary>Completes the operation. Contexts retain no pooled formatter storage.</summary>
    public readonly void Dispose() { }

    /// <summary>Immutable options for this operation.</summary>
    public readonly CborSerializerOptions Options => options;

    /// <summary>Serializes a child through its initialized formatter, charging one item.</summary>
    public void Serialize<TWriteBuffer, TReadBuffer, T>(ref TWriteBuffer buffer, T value, ICborFormatter<TWriteBuffer, TReadBuffer, T> formatter)
        where TWriteBuffer : struct, IWriteBuffer
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
        where TReadBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
    {
        ChargeItem();
        SerializeSameItem(ref buffer, value, formatter);
    }

    /// <summary>Delegates the current item without charging a second wire token.</summary>
    public void SerializeSameItem<TWriteBuffer, TReadBuffer, T>(ref TWriteBuffer buffer, T value, ICborFormatter<TWriteBuffer, TReadBuffer, T> formatter)
        where TWriteBuffer : struct, IWriteBuffer
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
        where TReadBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(formatter);
#else
        if (formatter is null) { throw new ArgumentNullException(nameof(formatter)); }
#endif
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

    internal void ChargeItem()
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
