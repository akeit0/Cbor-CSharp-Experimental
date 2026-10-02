using System.Buffers.Binary;
using System.Runtime.InteropServices;
using Cbor.Internal;

namespace Cbor;

/// <summary>Allocation-free CBOR operations on contiguous spans.</summary>
/// <remarks>Try-read methods return zero bytes consumed on failure. False from a try-write method
/// means the destination was too short and has not been changed. These operations do not track
/// container state, validate tag semantics, or enforce map-key uniqueness.</remarks>
public static class CborPrimitives
{
    /// <summary>Maximum size of one CBOR header.</summary>
    public const int MaxHeaderLength = CborEncoding.UInt64HeaderLength;

    /// <summary>Gets the preferred encoded size of an integer argument header.</summary>
    public static int GetHeaderLength(ulong argument) => argument < CborEncoding.DirectArgumentLimit ? CborEncoding.ImmediateHeaderLength :
        argument <= byte.MaxValue ? CborEncoding.UInt8HeaderLength : argument <= ushort.MaxValue ? CborEncoding.UInt16HeaderLength :
        argument <= uint.MaxValue ? CborEncoding.UInt32HeaderLength : CborEncoding.UInt64HeaderLength;

    /// <summary>Writes a preferred definite header for major types zero through six.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The major type is simple or outside the enum.</exception>
    public static bool TryWriteHeader(Span<byte> destination, CborMajorType majorType, ulong argument, out int bytesWritten)
    {
        if (majorType > CborMajorType.Tag)
        {
            throw new ArgumentOutOfRangeException(nameof(majorType));
        }

        bytesWritten = 0;
        int size = GetHeaderLength(argument);
        if (destination.Length < size)
        {
            return false;
        }

        CborTokenEncoder.UnsafeWriteExactHeader(ref MemoryMarshal.GetReference(destination), majorType, argument, size);

        bytesWritten = size;
        return true;
    }

    /// <summary>Writes an unsigned integer using its shortest representation.</summary>
    public static bool TryWriteUInt64(Span<byte> destination, ulong value, out int bytesWritten) =>
        TryWriteHeader(destination, CborMajorType.UnsignedInteger, value, out bytesWritten);

    /// <summary>Writes a signed integer, including Int64.MinValue, without overflowing.</summary>
    public static bool TryWriteInt64(Span<byte> destination, long value, out int bytesWritten) =>
        TryWriteHeader(destination, value >= 0 ? CborMajorType.UnsignedInteger : CborMajorType.NegativeInteger,
            value >= 0 ? (ulong)value : (ulong)~value, out bytesWritten);

    /// <summary>Writes the argument of a negative integer (-1 minus argument), including values below Int64.MinValue.</summary>
    public static bool TryWriteNegativeIntegerArgument(Span<byte> destination, ulong argument, out int bytesWritten) =>
        TryWriteHeader(destination, CborMajorType.NegativeInteger, argument, out bytesWritten);

    /// <summary>Writes a simple value. Values 24 through 31 are reserved and rejected.</summary>
    public static bool TryWriteSimpleValue(Span<byte> destination, byte value, out int bytesWritten)
    {
        if (value is >= CborEncoding.DirectArgumentLimit and < CborEncoding.MinimumExtendedSimpleValue)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        bytesWritten = 0;
        int size = value < CborEncoding.DirectArgumentLimit ? CborEncoding.ImmediateHeaderLength : CborEncoding.UInt8HeaderLength;
        if (destination.Length < size)
        {
            return false;
        }

        destination[0] = value < CborEncoding.DirectArgumentLimit ? (byte)(CborEncoding.SimplePrefix | value) : (byte)CborEncoding.ExtendedSimple;
        if (size == CborEncoding.UInt8HeaderLength)
        {
            destination[CborEncoding.ArgumentOffset] = value;
        }

        bytesWritten = size;
        return true;
    }

    /// <summary>Writes a Boolean.</summary>
    public static bool TryWriteBoolean(Span<byte> destination, bool value, out int bytesWritten) =>
        TryWriteSimpleValue(destination, value ? CborEncoding.TrueValue : CborEncoding.FalseValue, out bytesWritten);

    /// <summary>Writes CBOR null.</summary>
    public static bool TryWriteNull(Span<byte> destination, out int bytesWritten) =>
        TryWriteSimpleValue(destination, CborEncoding.NullValue, out bytesWritten);

    /// <summary>Writes CBOR undefined, which is distinct from null.</summary>
    public static bool TryWriteUndefined(Span<byte> destination, out int bytesWritten) =>
        TryWriteSimpleValue(destination, CborEncoding.UndefinedValue, out bytesWritten);

    /// <summary>Writes a definite byte string atomically, supporting overlapping source and destination spans.</summary>
    public static bool TryWriteByteString(Span<byte> destination, ReadOnlySpan<byte> value, out int bytesWritten) =>
        WriteStringSpan(destination, value, CborMajorType.ByteString, out bytesWritten);

    /// <summary>Writes validated UTF-8 atomically. Invalid input throws before changing the destination.</summary>
    public static bool TryWriteTextStringUtf8(Span<byte> destination, ReadOnlySpan<byte> value, out int bytesWritten)
    {
        if (!Utf8Validator.IsValid(value))
        {
            throw new ArgumentException("The text is not valid UTF-8.", nameof(value));
        }

        return WriteStringSpan(destination, value, CborMajorType.TextString, out bytesWritten);
    }

    /// <summary>Writes a float using the shortest width that preserves its value and zero sign. NaN becomes f97e00.</summary>
    public static bool TryWriteDouble(Span<byte> destination, double value, out int bytesWritten)
    {
        int length = FloatEncoding.GetPreferredWidth(value, out ulong bits);
        bytesWritten = 0;
        if (destination.Length < length)
        {
            return false;
        }

        CborTokenEncoder.UnsafeWriteFloatBits(ref MemoryMarshal.GetReference(destination), bits, length);
        bytesWritten = length;
        return true;
    }

    /// <summary>Writes a raw half-precision bit pattern. This preserves NaN payloads and may not be preferred.</summary>
    public static bool TryWriteHalfBits(Span<byte> destination, ushort bits, out int bytesWritten)
    {
        bytesWritten = 0;
        if (destination.Length < CborEncoding.Float16Length)
        {
            return false;
        }

        CborTokenEncoder.UnsafeWriteHalfBits(ref MemoryMarshal.GetReference(destination), bits);
        bytesWritten = CborEncoding.Float16Length;
        return true;
    }

    /// <summary>Writes an explicit single-precision representation without narrowing its width.</summary>
    public static bool TryWriteSingle(Span<byte> destination, float value, out int bytesWritten)
    {
        bytesWritten = 0;
        if (destination.Length < CborEncoding.Float32Length)
        {
            return false;
        }

        CborTokenEncoder.UnsafeWriteSingle(ref MemoryMarshal.GetReference(destination), value);
        bytesWritten = CborEncoding.Float32Length;
        return true;
    }

    /// <summary>Writes an explicit double-precision representation without narrowing its width.</summary>
    public static bool TryWriteDoublePrecision(Span<byte> destination, double value, out int bytesWritten)
    {
        bytesWritten = 0;
        if (destination.Length < CborEncoding.Float64Length)
        {
            return false;
        }

        CborTokenEncoder.UnsafeWriteDoublePrecision(ref MemoryMarshal.GetReference(destination), value);
        bytesWritten = CborEncoding.Float64Length;
        return true;
    }

    /// <summary>Decodes one header without reading payloads or children. A break marker is reported for contextual interpretation.</summary>
    public static CborDecodeResult TryReadHeader(ReadOnlySpan<byte> source, out CborHeader header)
    {
        header = default;
        if (source.IsEmpty)
        {
            return CborDecodeResult.NeedMoreData;
        }

        byte initial = source[0];
        var major = (CborMajorType)(initial >> CborEncoding.MajorTypeShift);
        byte additional = (byte)(initial & CborEncoding.AdditionalInformationMask);
        if (additional is >= CborEncoding.FirstReservedArgument and <= CborEncoding.LastReservedArgument)
        {
            return CborDecodeResult.InvalidData;
        }

        if (additional == CborEncoding.IndefiniteArgument)
        {
            if (major is CborMajorType.UnsignedInteger or CborMajorType.NegativeInteger or CborMajorType.Tag)
            {
                return CborDecodeResult.InvalidData;
            }

            header = new CborHeader(major, additional, 0, CborEncoding.ImmediateHeaderLength);
            return CborDecodeResult.Success;
        }

        int size = CborTokenDecoder.EncodedLength(initial);
        if (source.Length < size)
        {
            return CborDecodeResult.NeedMoreData;
        }

        ulong argument = size switch
        {
            CborEncoding.ImmediateHeaderLength => additional,
            CborEncoding.UInt8HeaderLength => source[CborEncoding.ArgumentOffset],
            CborEncoding.UInt16HeaderLength => BinaryPrimitives.ReadUInt16BigEndian(source.Slice(CborEncoding.ArgumentOffset)),
            CborEncoding.UInt32HeaderLength => BinaryPrimitives.ReadUInt32BigEndian(source.Slice(CborEncoding.ArgumentOffset)),
            _ => BinaryPrimitives.ReadUInt64BigEndian(source.Slice(CborEncoding.ArgumentOffset)),
        };
        if (major == CborMajorType.Simple && additional == CborEncoding.UInt8Argument && argument < CborEncoding.MinimumExtendedSimpleValue)
        {
            return CborDecodeResult.InvalidData;
        }

        header = new CborHeader(major, additional, argument, size);
        return CborDecodeResult.Success;
    }

    /// <summary>Reads a UInt64. Wider well-formed encodings are accepted.</summary>
    public static CborDecodeResult TryReadUInt64(ReadOnlySpan<byte> source, out ulong value, out int bytesConsumed) =>
        ReadArgument(source, CborMajorType.UnsignedInteger, out value, out bytesConsumed);

    /// <summary>Reads the full unsigned argument of a CBOR negative integer (-1 minus argument).</summary>
    public static CborDecodeResult TryReadNegativeIntegerArgument(ReadOnlySpan<byte> source, out ulong value, out int bytesConsumed) =>
        ReadArgument(source, CborMajorType.NegativeInteger, out value, out bytesConsumed);

    /// <summary>Reads a signed integer. CBOR integers outside Int64 return Overflow without consumption.</summary>
    public static CborDecodeResult TryReadInt64(ReadOnlySpan<byte> source, out long value, out int bytesConsumed)
    {
        value = 0;
        bytesConsumed = 0;
        if (source.IsEmpty)
        {
            return CborDecodeResult.NeedMoreData;
        }

        byte initial = source[0];
        if (initial >= CborEncoding.ByteStringPrefix)
        {
            return CborTokenDecoder.Mismatch(source);
        }

        ulong argument = (ulong)(initial & CborEncoding.AdditionalInformationMask);
        int length = CborEncoding.ImmediateHeaderLength;
        if (argument >= CborEncoding.DirectArgumentLimit)
        {
            var result = CborTokenDecoder.ReadExtendedArgument(source, out argument, out length);
            if (result != CborDecodeResult.Success)
            {
                return result;
            }
        }

        if (argument > long.MaxValue)
        {
            return CborDecodeResult.Overflow;
        }

        value = initial < CborEncoding.NegativeIntegerPrefix ? (long)argument : ~(long)argument;
        bytesConsumed = length;
        return CborDecodeResult.Success;
    }

    /// <summary>Reads a float of any of the three widths into Double. Integers are not implicitly converted.</summary>
    public static CborDecodeResult TryReadDouble(ReadOnlySpan<byte> source, out double value, out int bytesConsumed)
    {
        return CborTokenDecoder.ReadFloat(source, out value, out bytesConsumed);
    }

    /// <summary>Reads a simple value, including null/undefined. Floats and break return TypeMismatch.</summary>
    public static CborDecodeResult TryReadSimpleValue(ReadOnlySpan<byte> source, out byte value, out int bytesConsumed)
    {
        value = 0;
        bytesConsumed = 0;
        if (source.IsEmpty)
        {
            return CborDecodeResult.NeedMoreData;
        }

        byte initial = source[0];
        if (initial is >= CborEncoding.SimplePrefix and <= CborEncoding.Undefined)
        {
            value = (byte)(initial & CborEncoding.AdditionalInformationMask);
            bytesConsumed = 1;
            return CborDecodeResult.Success;
        }

        if (initial != CborEncoding.ExtendedSimple)
        {
            return CborTokenDecoder.Mismatch(source);
        }

        if (source.Length < CborEncoding.UInt8HeaderLength)
        {
            return CborDecodeResult.NeedMoreData;
        }

        if (source[CborEncoding.ArgumentOffset] < CborEncoding.MinimumExtendedSimpleValue)
        {
            return CborDecodeResult.InvalidData;
        }

        value = source[CborEncoding.ArgumentOffset];
        bytesConsumed = CborEncoding.UInt8HeaderLength;
        return CborDecodeResult.Success;
    }

    /// <summary>Reads a Boolean.</summary>
    public static CborDecodeResult TryReadBoolean(ReadOnlySpan<byte> source, out bool value, out int bytesConsumed)
    {
        value = false;
        bytesConsumed = 0;
        if (source.IsEmpty)
        {
            return CborDecodeResult.NeedMoreData;
        }

        byte initial = source[0];
        if (initial is not (CborEncoding.False or CborEncoding.True))
        {
            return CborTokenDecoder.Mismatch(source);
        }

        value = initial == CborEncoding.True;
        bytesConsumed = 1;
        return CborDecodeResult.Success;
    }

    /// <summary>Reads a definite byte string as a borrowed span. Indefinite strings return TypeMismatch.</summary>
    public static CborDecodeResult TryReadByteString(ReadOnlySpan<byte> source, out ReadOnlySpan<byte> value, out int bytesConsumed) =>
        ReadStringSpan(source, CborMajorType.ByteString, out value, out bytesConsumed);

    /// <summary>Reads and strictly validates a definite text string as borrowed UTF-8 bytes.</summary>
    public static CborDecodeResult TryReadTextStringUtf8(ReadOnlySpan<byte> source, out ReadOnlySpan<byte> value, out int bytesConsumed) =>
        ReadStringSpan(source, CborMajorType.TextString, out value, out bytesConsumed);

    private static CborDecodeResult ReadArgument(ReadOnlySpan<byte> source, CborMajorType major, out ulong value, out int consumed)
    {
        value = 0;
        consumed = 0;
        if (source.IsEmpty)
        {
            return CborDecodeResult.NeedMoreData;
        }

        byte initial = source[0];
        if ((initial >> CborEncoding.MajorTypeShift) != (byte)major)
        {
            return CborTokenDecoder.Mismatch(source);
        }

        byte additional = (byte)(initial & CborEncoding.AdditionalInformationMask);
        if (additional >= CborEncoding.DirectArgumentLimit)
        {
            return CborTokenDecoder.ReadExtendedArgument(source, out value, out consumed);
        }

        value = additional;
        consumed = 1;
        return CborDecodeResult.Success;
    }

    private static bool WriteStringSpan(Span<byte> destination, ReadOnlySpan<byte> value, CborMajorType major, out int written)
    {
        written = 0;
        int headerLength = GetHeaderLength((ulong)value.Length);
        if (destination.Length < headerLength || value.Length > destination.Length - headerLength)
        {
            return false;
        }

        // Copy first: publishing the header first could overwrite an overlapping source.
        value.CopyTo(destination.Slice(headerLength));
        TryWriteHeader(destination, major, (ulong)value.Length, out _);
        written = headerLength + value.Length;
        return true;
    }

    private static CborDecodeResult ReadStringSpan(ReadOnlySpan<byte> source, CborMajorType major, out ReadOnlySpan<byte> value, out int consumed)
    {
        value = default;
        consumed = 0;
        if (!source.IsEmpty && source[0] == (((byte)major << CborEncoding.MajorTypeShift) | CborEncoding.IndefiniteArgument))
        {
            return CborDecodeResult.TypeMismatch;
        }

        var result = ReadArgument(source, major, out ulong length, out int headerLength);
        if (result != CborDecodeResult.Success)
        {
            return result;
        }

        if (length > (ulong)(source.Length - headerLength))
        {
            return CborDecodeResult.NeedMoreData;
        }

        var payload = source.Slice(headerLength, (int)length);
        if (major == CborMajorType.TextString && !Utf8Validator.IsValid(payload))
        {
            return CborDecodeResult.InvalidData;
        }

        value = payload;
        consumed = headerLength + payload.Length;
        return CborDecodeResult.Success;
    }
}
