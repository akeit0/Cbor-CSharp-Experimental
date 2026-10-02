using Cbor.Internal;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SerializerFoundation;

namespace Cbor;

/// <summary>CBOR output operations for single-owner Foundation buffers, always passed by reference.</summary>
/// <remarks>Callers manage container balance and call Flush/Dispose on their buffer.
/// Integer/header operations may use nine contiguous scratch bytes when available, committing only
/// their encoded length. Fixed destinations need only the actual encoded size.
/// Header operations do not sort map keys or validate tag semantics.</remarks>
public static class CborWriteBufferExtensions
{

    /// <summary>Writes an unsigned integer.</summary>
    public static void WriteUInt64<TBuffer>(this ref TBuffer buffer, ulong value)
        where TBuffer : struct, IWriteBuffer
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
    {
        WriteHeader(ref buffer, CborMajorType.UnsignedInteger, value);
    }

    /// <summary>Writes a signed integer.</summary>
    public static void WriteInt64<TBuffer>(this ref TBuffer buffer, long value)
        where TBuffer : struct, IWriteBuffer
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
    {
        WriteHeader(ref buffer, value >= 0 ? CborMajorType.UnsignedInteger : CborMajorType.NegativeInteger,
            value >= 0 ? (ulong)value : (ulong)~value);
    }

    /// <summary>Writes a negative integer from its full unsigned argument (-1 minus argument).</summary>
    public static void WriteNegativeIntegerArgument<TBuffer>(this ref TBuffer buffer, ulong argument)
        where TBuffer : struct, IWriteBuffer
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
    {
        WriteHeader(ref buffer, CborMajorType.NegativeInteger, argument);
    }

    /// <summary>Writes a floating-point value using preferred width and canonical NaN.</summary>
    public static void WriteDouble<TBuffer>(this ref TBuffer buffer, double value)
        where TBuffer : struct, IWriteBuffer
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
    {
        int written = FloatEncoding.GetPreferredWidth(value, out ulong bits);
        CborTokenEncoder.UnsafeWriteFloatBits(ref buffer.GetReference(written), bits, written);
        buffer.Advance(written);
    }

    /// <summary>Writes an explicitly single-precision float.</summary>
    public static void WriteSingle<TBuffer>(this ref TBuffer buffer, float value)
        where TBuffer : struct, IWriteBuffer
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
    {
        CborTokenEncoder.UnsafeWriteSingle(ref buffer.GetReference(CborEncoding.Float32Length), value);
        buffer.Advance(CborEncoding.Float32Length);
    }

    /// <summary>Writes an explicitly double-precision float.</summary>
    public static void WriteDoublePrecision<TBuffer>(this ref TBuffer buffer, double value)
        where TBuffer : struct, IWriteBuffer
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
    {
        CborTokenEncoder.UnsafeWriteDoublePrecision(ref buffer.GetReference(CborEncoding.Float64Length), value);
        buffer.Advance(CborEncoding.Float64Length);
    }

    /// <summary>Writes a raw half-precision bit pattern.</summary>
    public static void WriteHalfBits<TBuffer>(this ref TBuffer buffer, ushort bits)
        where TBuffer : struct, IWriteBuffer
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
    {
        CborTokenEncoder.UnsafeWriteHalfBits(ref buffer.GetReference(CborEncoding.Float16Length), bits);
        buffer.Advance(CborEncoding.Float16Length);
    }

    /// <summary>Writes a simple value, rejecting reserved values 24 through 31.</summary>
    public static void WriteSimpleValue<TBuffer>(this ref TBuffer buffer, byte value)
        where TBuffer : struct, IWriteBuffer
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
    {
        if (value is >= CborEncoding.DirectArgumentLimit and < CborEncoding.MinimumExtendedSimpleValue)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        if (value < CborEncoding.DirectArgumentLimit)
        {
            buffer.GetReference(CborEncoding.ImmediateHeaderLength) = (byte)(CborEncoding.SimplePrefix | value);
            buffer.Advance(CborEncoding.ImmediateHeaderLength);
        }
        else
        {
            ref byte destination = ref buffer.GetReference(CborEncoding.UInt8HeaderLength);
            destination = CborEncoding.ExtendedSimple;
            Unsafe.Add(ref destination, CborEncoding.ArgumentOffset) = value;
            buffer.Advance(CborEncoding.UInt8HeaderLength);
        }
    }

    /// <summary>Writes a Boolean.</summary>
    public static void WriteBoolean<TBuffer>(this ref TBuffer buffer, bool value)
        where TBuffer : struct, IWriteBuffer
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
    {
        buffer.GetReference(CborEncoding.ImmediateHeaderLength) = value ? (byte)CborEncoding.True : (byte)CborEncoding.False;
        buffer.Advance(CborEncoding.ImmediateHeaderLength);
    }

    /// <summary>Writes CBOR null.</summary>
    public static void WriteNull<TBuffer>(this ref TBuffer buffer)
        where TBuffer : struct, IWriteBuffer
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
    {
        buffer.GetReference(CborEncoding.ImmediateHeaderLength) = CborEncoding.Null;
        buffer.Advance(CborEncoding.ImmediateHeaderLength);
    }

    /// <summary>Writes CBOR undefined.</summary>
    public static void WriteUndefined<TBuffer>(this ref TBuffer buffer)
        where TBuffer : struct, IWriteBuffer
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
    {
        buffer.GetReference(CborEncoding.ImmediateHeaderLength) = CborEncoding.Undefined;
        buffer.Advance(CborEncoding.ImmediateHeaderLength);
    }

    /// <summary>Writes a preferred definite header for major types zero through six.</summary>
    public static void WriteHeader<TBuffer>(this ref TBuffer buffer, CborMajorType majorType, ulong argument)
        where TBuffer : struct, IWriteBuffer
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
    {
        if (majorType > CborMajorType.Tag)
        {
            throw new ArgumentOutOfRangeException(nameof(majorType));
        }

        if (argument < CborEncoding.DirectArgumentLimit)
        {
            buffer.GetReference(CborEncoding.ImmediateHeaderLength) = (byte)(((byte)majorType << CborEncoding.MajorTypeShift) | (byte)argument);
            buffer.Advance(CborEncoding.ImmediateHeaderLength);
            return;
        }

        var window = buffer.GetSpan();
        if (window.Length >= CborPrimitives.MaxHeaderLength)
        {
            int written = CborTokenEncoder.UnsafeWriteHeader(ref MemoryMarshal.GetReference(window), majorType, argument);
            buffer.Advance(written);
            return;
        }

        WriteHeaderExact(ref buffer, majorType, argument);
    }

    /// <summary>Writes an array header; null selects an indefinite-length array.</summary>
    public static void WriteArrayHeader<TBuffer>(this ref TBuffer buffer, ulong? count)
        where TBuffer : struct, IWriteBuffer
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
    {
        if (count.HasValue)
        {
            WriteHeader(ref buffer, CborMajorType.Array, count.Value);
        }
        else
        {
            WriteIndefinite(ref buffer, CborMajorType.Array);
        }
    }

    /// <summary>Writes a map header; count means pairs, and null selects an indefinite-length map.</summary>
    public static void WriteMapHeader<TBuffer>(this ref TBuffer buffer, ulong? count)
        where TBuffer : struct, IWriteBuffer
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
    {
        if (count.HasValue)
        {
            WriteHeader(ref buffer, CborMajorType.Map, count.Value);
        }
        else
        {
            WriteIndefinite(ref buffer, CborMajorType.Map);
        }
    }

    /// <summary>Writes a semantic tag header. Exactly one data item must follow.</summary>
    public static void WriteTag<TBuffer>(this ref TBuffer buffer, ulong tag)
        where TBuffer : struct, IWriteBuffer
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
        => WriteHeader(ref buffer, CborMajorType.Tag, tag);

    /// <summary>Starts an indefinite byte string. Only definite byte-string chunks may follow.</summary>
    public static void WriteStartIndefiniteByteString<TBuffer>(this ref TBuffer buffer)
        where TBuffer : struct, IWriteBuffer
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
        => WriteIndefinite(ref buffer, CborMajorType.ByteString);

    /// <summary>Starts an indefinite text string. Every definite text chunk must contain complete UTF-8.</summary>
    public static void WriteStartIndefiniteTextString<TBuffer>(this ref TBuffer buffer)
        where TBuffer : struct, IWriteBuffer
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
        => WriteIndefinite(ref buffer, CborMajorType.TextString);

    /// <summary>Writes a break marker to end an indefinite string, array, or map.</summary>
    public static void WriteBreak<TBuffer>(this ref TBuffer buffer)
        where TBuffer : struct, IWriteBuffer
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
    {
        buffer.GetReference(CborEncoding.ImmediateHeaderLength) = CborEncoding.Break;
        buffer.Advance(CborEncoding.ImmediateHeaderLength);
    }

    /// <summary>Writes a definite byte string. Its payload is copied without requiring a contiguous destination.</summary>
    public static void WriteByteString<TBuffer>(this ref TBuffer buffer, scoped ReadOnlySpan<byte> value)
        where TBuffer : struct, IWriteBuffer
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
    {
        WriteHeader(ref buffer, CborMajorType.ByteString, (ulong)value.Length);
        WritePayload(ref buffer, value);
    }

    /// <summary>Validates UTF-8 before writing a definite text string. Invalid text leaves the buffer unchanged.</summary>
    public static void WriteTextStringUtf8<TBuffer>(this ref TBuffer buffer, scoped ReadOnlySpan<byte> value)
        where TBuffer : struct, IWriteBuffer
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
    {
        if (!Utf8Validator.IsValid(value))
        {
            throw new ArgumentException("The text is not valid UTF-8.", nameof(value));
        }

        WriteHeader(ref buffer, CborMajorType.TextString, (ulong)value.Length);
        WritePayload(ref buffer, value);
    }

    /// <summary>Writes a definite text string directly into the destination, rejecting unpaired UTF-16 surrogates.</summary>
    public static void WriteTextString<TBuffer>(this ref TBuffer buffer, string value)
        where TBuffer : struct, IWriteBuffer
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(value);
#else
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }
#endif

        // ByteCount validates UTF-16 before publishing a header; no temporary UTF-8 array is allocated.
        int byteCount = CborTextEncoding.StrictUtf8.GetByteCount(value);
        WriteTextStringKnownLength(ref buffer, value, byteCount);
    }

    // The caller must obtain byteCount through strict GetByteCount before publishing any bytes.
    internal static unsafe void WriteTextStringKnownLength<TBuffer>(ref TBuffer buffer, string value, int byteCount)
        where TBuffer : struct, IWriteBuffer
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
    {
        WriteHeader(ref buffer, CborMajorType.TextString, (ulong)byteCount);
        if (byteCount == 0)
        {
            return;
        }

        Span<byte> destination = buffer.GetSpan(byteCount);
#if NETSTANDARD2_0
        fixed (char* source = value)
        fixed (byte* target = destination)
        {
            CborTextEncoding.StrictUtf8.GetBytes(source, value.Length, target, byteCount);
        }
#else
        CborTextEncoding.StrictUtf8.GetBytes(value.AsSpan(), destination);
#endif
        buffer.Advance(byteCount);
    }

    private static void WriteIndefinite<TBuffer>(ref TBuffer buffer, CborMajorType type)
        where TBuffer : struct, IWriteBuffer
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
    {
        buffer.GetReference(CborEncoding.ImmediateHeaderLength) = (byte)(((byte)type << CborEncoding.MajorTypeShift) | CborEncoding.IndefiniteArgument);
        buffer.Advance(CborEncoding.ImmediateHeaderLength);
    }

    private static void WritePayload<TBuffer>(ref TBuffer buffer, scoped ReadOnlySpan<byte> payload)
        where TBuffer : struct, IWriteBuffer
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
    {
        while (!payload.IsEmpty)
        {
            Span<byte> destination = buffer.GetSpan();
            int count = Math.Min(destination.Length, payload.Length);
            payload.Slice(0, count).CopyTo(destination);
            buffer.Advance(count);
            payload = payload.Slice(count);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void WriteHeaderExact<TBuffer>(ref TBuffer buffer, CborMajorType major, ulong argument)
        where TBuffer : struct, IWriteBuffer
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
    {
        int length = CborPrimitives.GetHeaderLength(argument);
        CborTokenEncoder.UnsafeWriteExactHeader(ref buffer.GetReference(length), major, argument, length);
        buffer.Advance(length);
    }
}
