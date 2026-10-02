using System.Text;
using System.Runtime.CompilerServices;
using Cbor.Internal;
using SerializerFoundation;

namespace Cbor;

/// <summary>CBOR token operations over Foundation buffers, always passed by reference.</summary>
/// <remarks>Scalar and header errors do not consume input. String materialization may consume
/// its header before rejecting invalid UTF-8. These operations do not maintain container state.
/// Validate an enclosing item when full structural validity is required.</remarks>
public static class CborReadBufferExtensions
{

    /// <summary>Inspects a header without consuming it.</summary>
    public static CborHeader PeekHeader<TBuffer>(this ref TBuffer buffer)
        where TBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
    {
        var result = TryPeekHeader(ref buffer, out var header);
        if (result != CborDecodeResult.Success)
        {
            CborError.Throw(result);
        }

        return header;
    }

    /// <summary>Consumes one header. Payloads and children are left unread.</summary>
    public static CborHeader ReadHeader<TBuffer>(this ref TBuffer buffer)
        where TBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
    {
        var header = PeekHeader(ref buffer);
        buffer.Advance(header.EncodedLength);
        return header;
    }

    /// <summary>Reads an unsigned integer without consuming input on a token error.</summary>
    public static ulong ReadUInt64<TBuffer>(this ref TBuffer buffer)
        where TBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
        => ReadArgument(ref buffer, CborMajorType.UnsignedInteger);

    /// <summary>Reads the complete argument of a negative integer (-1 minus argument).</summary>
    public static ulong ReadNegativeIntegerArgument<TBuffer>(this ref TBuffer buffer)
        where TBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
        => ReadArgument(ref buffer, CborMajorType.NegativeInteger);

    /// <summary>Reads a signed integer, throwing OverflowException for CBOR integers outside Int64.</summary>
    public static long ReadInt64<TBuffer>(this ref TBuffer buffer)
        where TBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
    {
        var span = buffer.GetUnreadSpan();
        if (!span.IsEmpty && span[0] < CborEncoding.ByteStringPrefix &&
            CborTokenDecoder.TryReadContiguousArgument(span, (byte)(span[0] & CborEncoding.NegativeIntegerPrefix), out ulong argument, out int length) && argument <= long.MaxValue)
        {
            byte initial = span[0];
            buffer.Advance(length);
            return initial < CborEncoding.NegativeIntegerPrefix ? (long)argument : ~(long)argument;
        }

        return ReadInt64Cold(ref buffer);
    }

    /// <summary>Reads any floating-point width into Double, preserving the sign of zero.</summary>
    public static double ReadDouble<TBuffer>(this ref TBuffer buffer)
        where TBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
    {
        var result = CborPrimitives.TryReadDouble(GetTokenSpan(ref buffer), out double value, out int length);
        if (result != CborDecodeResult.Success)
        {
            CborError.Throw(result);
        }

        buffer.Advance(length);
        return value;
    }

    /// <summary>Reads a simple value, including null and undefined, without accepting floats or break.</summary>
    public static byte ReadSimpleValue<TBuffer>(this ref TBuffer buffer)
        where TBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
    {
        var span = buffer.GetUnreadSpan();
        if (!span.IsEmpty && span[0] is >= CborEncoding.SimplePrefix and <= CborEncoding.Undefined)
        {
            byte value = (byte)(span[0] & CborEncoding.AdditionalInformationMask);
            buffer.Advance(CborEncoding.ImmediateHeaderLength);
            return value;
        }

        return ReadSimpleValueCold(ref buffer);
    }

    /// <summary>Reads a Boolean without consuming a mismatched token.</summary>
    public static bool ReadBoolean<TBuffer>(this ref TBuffer buffer)
        where TBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
    {
        var span = buffer.GetUnreadSpan();
        if (!span.IsEmpty && span[0] is CborEncoding.False or CborEncoding.True)
        {
            bool value = span[0] == CborEncoding.True;
            buffer.Advance(CborEncoding.ImmediateHeaderLength);
            return value;
        }

        return ReadBooleanCold(ref buffer);
    }

    /// <summary>Consumes CBOR null, rejecting all other tokens without consumption.</summary>
    public static void ReadNull<TBuffer>(this ref TBuffer buffer)
        where TBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
        => ReadExpectedSimple(ref buffer, CborEncoding.Null);

    /// <summary>Consumes CBOR undefined, rejecting all other tokens without consumption.</summary>
    public static void ReadUndefined<TBuffer>(this ref TBuffer buffer)
        where TBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
        => ReadExpectedSimple(ref buffer, CborEncoding.Undefined);

    /// <summary>Reads an array header. Null means indefinite length.</summary>
    public static ulong? ReadArrayHeader<TBuffer>(this ref TBuffer buffer)
        where TBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
        => ReadLength(ref buffer, CborMajorType.Array);

    /// <summary>Reads a map header. A definite count means pairs; null means indefinite length.</summary>
    public static ulong? ReadMapHeader<TBuffer>(this ref TBuffer buffer)
        where TBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
        => ReadLength(ref buffer, CborMajorType.Map);

    /// <summary>Reads a tag header. Exactly one tagged item remains unread.</summary>
    public static ulong ReadTag<TBuffer>(this ref TBuffer buffer)
        where TBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
        => ReadArgument(ref buffer, CborMajorType.Tag);

    /// <summary>Consumes a break marker when present. The caller must enforce its enclosing context.</summary>
    public static bool TryReadBreak<TBuffer>(this ref TBuffer buffer)
        where TBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
    {
        var span = buffer.GetUnreadSpan();
        if (span.IsEmpty || span[0] != CborEncoding.Break)
        {
            return false;
        }

        buffer.Advance(CborEncoding.ImmediateHeaderLength);
        return true;
    }

    /// <summary>Materializes a definite byte string, checking input availability and allocation bounds before allocation.</summary>
    public static byte[] ReadByteString<TBuffer>(this ref TBuffer buffer, int maxLength = 16 * 1024 * 1024)
        where TBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
        => ReadStringPayload(ref buffer, CborMajorType.ByteString, maxLength);

    /// <summary>Materializes a definite text string with strict UTF-8 validation and a byte-length allocation bound.</summary>
    public static unsafe string ReadTextString<TBuffer>(this ref TBuffer buffer, int maxByteLength = 16 * 1024 * 1024)
        where TBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
    {
        var header = ValidateStringHeader(ref buffer, CborMajorType.TextString, maxByteLength);
        int length = (int)header.Argument;
        buffer.Advance(header.EncodedLength);
        if (length == 0)
        {
            return string.Empty;
        }

        // Foundation supplies a borrowed contiguous window, renting only across a segment seam.
        // Decode directly into the final string rather than allocating a temporary byte array.
        if (!buffer.TryGetSpan(length, out var span))
        {
            CborError.Throw(CborDecodeResult.NeedMoreData);
        }

        string value;
        try
        {
#if NETSTANDARD2_0
            fixed (byte* pointer = span)
            {
                value = CborTextEncoding.StrictUtf8.GetString(pointer, length);
            }
#else
            value = CborTextEncoding.StrictUtf8.GetString(span.Slice(0, length));
#endif
        }
        catch (DecoderFallbackException error)
        {
            throw new InvalidDataException("The CBOR text string contains invalid UTF-8.", error);
        }

        buffer.Advance(length);
        return value;
    }

    /// <summary>Skips one structurally valid item under the supplied limits.</summary>
    /// <remarks>On failure bytesConsumed reports the prefix already consumed; this method cannot rewind a Foundation buffer.
    /// It checks UTF-8 and structure, but does not establish map-key uniqueness or registered-tag semantics.</remarks>
    public static CborDecodeResult TrySkipValue<TBuffer>(this ref TBuffer buffer, out long bytesConsumed, CborReaderOptions? options = null)
        where TBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
    {
        long start = buffer.BytesConsumed;
        var result = CborScanner.Scan(ref buffer, options ?? CborReaderOptions.Default);
        bytesConsumed = buffer.BytesConsumed - start;
        return result;
    }

    internal static CborDecodeResult TryPeekHeader<TBuffer>(ref TBuffer buffer, out CborHeader header)
        where TBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
    {
        var span = buffer.GetUnreadSpan();
        var result = CborPrimitives.TryReadHeader(span, out header);
        if (result != CborDecodeResult.NeedMoreData || span.IsEmpty)
        {
            return result;
        }

        int additional = span[0] & CborEncoding.AdditionalInformationMask;
        int size = additional == CborEncoding.UInt8Argument ? CborEncoding.UInt8HeaderLength :
            additional == CborEncoding.UInt16Argument ? CborEncoding.UInt16HeaderLength :
            additional == CborEncoding.UInt32Argument ? CborEncoding.UInt32HeaderLength : CborEncoding.UInt64HeaderLength;
        if (!buffer.TryGetSpan(size, out span))
        {
            return CborDecodeResult.NeedMoreData;
        }

        return CborPrimitives.TryReadHeader(span, out header);
    }

    private static ulong ReadArgument<TBuffer>(ref TBuffer buffer, CborMajorType major)
        where TBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
    {
        var span = buffer.GetUnreadSpan();
        if (CborTokenDecoder.TryReadContiguousArgument(span, (byte)((byte)major << CborEncoding.MajorTypeShift), out ulong value, out int length))
        {
            buffer.Advance(length);
            return value;
        }

        return ReadArgumentCold(ref buffer, major);
    }

    private static ulong? ReadLength<TBuffer>(ref TBuffer buffer, CborMajorType major)
        where TBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
    {
        var header = PeekLengthHeader(ref buffer, major);

        buffer.Advance(header.EncodedLength);
        return header.IsIndefiniteLength ? null : header.Argument;
    }

    private static void ReadExpectedSimple<TBuffer>(ref TBuffer buffer, byte expected)
        where TBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
    {
        var span = buffer.GetUnreadSpan();
        if (span.IsEmpty || span[0] != expected)
        {
            CborError.Throw(CborTokenDecoder.Mismatch(GetTokenSpan(ref buffer)));
        }

        buffer.Advance(CborEncoding.ImmediateHeaderLength);
    }

    private static byte[] ReadStringPayload<TBuffer>(ref TBuffer buffer, CborMajorType major, int maxLength)
        where TBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
    {
        var header = ValidateStringHeader(ref buffer, major, maxLength);
        byte[] payload = new byte[(int)header.Argument];
        buffer.Advance(header.EncodedLength);
        buffer.CopyTo(payload);
        buffer.Advance(payload.Length);
        return payload;
    }

    private static CborHeader ValidateStringHeader<TBuffer>(ref TBuffer buffer, CborMajorType major, int maxLength)
        where TBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
    {
#if NET8_0_OR_GREATER
        ArgumentOutOfRangeException.ThrowIfNegative(maxLength);
#else
        if (maxLength < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxLength));
        }
#endif

        var header = PeekLengthHeader(ref buffer, major);
        if (header.IsIndefiniteLength)
        {
            CborError.Throw(CborDecodeResult.TypeMismatch);
        }

        if (header.Argument > (ulong)maxLength)
        {
            CborError.Throw(CborDecodeResult.LimitExceeded);
        }

        if (header.Argument > (ulong)(buffer.BytesRemaining - header.EncodedLength))
        {
            CborError.Throw(CborDecodeResult.NeedMoreData);
        }

        return header;
    }

    internal static CborHeader PeekLengthHeader<TBuffer>(ref TBuffer buffer, CborMajorType major)
        where TBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
    {
        var span = buffer.GetUnreadSpan();
        if (!span.IsEmpty && (span[0] >> CborEncoding.MajorTypeShift) == (byte)major)
        {
            byte additional = (byte)(span[0] & CborEncoding.AdditionalInformationMask);
            if (additional < CborEncoding.DirectArgumentLimit || additional == CborEncoding.IndefiniteArgument)
            {
                return new CborHeader(major, additional, additional == CborEncoding.IndefiniteArgument ? 0UL : additional, CborEncoding.ImmediateHeaderLength);
            }

            var result = CborTokenDecoder.ReadExtendedArgument(GetTokenSpan(ref buffer), out ulong argument, out int length);
            if (result != CborDecodeResult.Success)
            {
                CborError.Throw(result);
            }

            return new CborHeader(major, additional, argument, length);
        }

        CborError.Throw(CborTokenDecoder.Mismatch(GetTokenSpan(ref buffer)));
        return default;
    }

    // Called for extended tokens and errors. Foundation handles a segment seam only here.
    internal static ReadOnlySpan<byte> GetTokenSpan<TBuffer>(ref TBuffer buffer)
        where TBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
    {
        var span = buffer.GetUnreadSpan();
        if (span.IsEmpty)
        {
            return span;
        }

        int size = CborTokenDecoder.EncodedLength(span[0]);
        if (span.Length >= size)
        {
            return span;
        }

        // A rebuffering attempt may invalidate the original borrowed window, even on failure.
        return buffer.TryGetSpan(size, out var contiguous) ? contiguous : default;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static long ReadInt64Cold<TBuffer>(ref TBuffer buffer)
        where TBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
    {
        var result = CborPrimitives.TryReadInt64(GetTokenSpan(ref buffer), out long value, out int length);
        if (result != CborDecodeResult.Success)
        {
            CborError.Throw(result);
        }
        buffer.Advance(length);
        return value;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ulong ReadArgumentCold<TBuffer>(ref TBuffer buffer, CborMajorType major)
        where TBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
    {
        var span = GetTokenSpan(ref buffer);
        if (span.IsEmpty || (span[0] >> CborEncoding.MajorTypeShift) != (byte)major)
        {
            CborError.Throw(CborTokenDecoder.Mismatch(span));
        }

        var result = CborTokenDecoder.ReadExtendedArgument(span, out ulong value, out int length);
        if (result != CborDecodeResult.Success)
        {
            CborError.Throw(result);
        }
        buffer.Advance(length);
        return value;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static byte ReadSimpleValueCold<TBuffer>(ref TBuffer buffer)
        where TBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
    {
        var result = CborPrimitives.TryReadSimpleValue(GetTokenSpan(ref buffer), out byte value, out int length);
        if (result != CborDecodeResult.Success)
        {
            CborError.Throw(result);
        }
        buffer.Advance(length);
        return value;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool ReadBooleanCold<TBuffer>(ref TBuffer buffer)
        where TBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
    {
        var result = CborPrimitives.TryReadBoolean(GetTokenSpan(ref buffer), out bool value, out int length);
        if (result != CborDecodeResult.Success)
        {
            CborError.Throw(result);
        }
        buffer.Advance(length);
        return value;
    }
}
