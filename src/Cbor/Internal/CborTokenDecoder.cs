using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace Cbor.Internal;

// Typed readers inspect the initial byte before entering these extended-width paths.
// The general header decoder is used only to classify a mismatched/malformed token.
internal static class CborTokenDecoder
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int EncodedLength(byte initial) => (initial & CborEncoding.AdditionalInformationMask) switch
    {
        CborEncoding.UInt8Argument => CborEncoding.UInt8HeaderLength,
        CborEncoding.UInt16Argument => CborEncoding.UInt16HeaderLength,
        CborEncoding.UInt32Argument => CborEncoding.UInt32HeaderLength,
        CborEncoding.UInt64Argument => CborEncoding.UInt64HeaderLength,
        _ => CborEncoding.ImmediateHeaderLength,
    };

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static CborDecodeResult Mismatch(ReadOnlySpan<byte> source)
    {
        var result = CborPrimitives.TryReadHeader(source, out _);
        return result == CborDecodeResult.Success ? CborDecodeResult.TypeMismatch : result;
    }

    // Caller has already checked the major type. No major-type dispatch or CborHeader construction.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static CborDecodeResult ReadExtendedArgument(ReadOnlySpan<byte> source, out ulong argument, out int length)
    {
        argument = 0;
        length = 0;
        if (source.IsEmpty)
        {
            return CborDecodeResult.NeedMoreData;
        }
        int additional = source[0] & CborEncoding.AdditionalInformationMask;
        if (additional > CborEncoding.UInt64Argument)
        {
            return CborDecodeResult.InvalidData;
        }

        int size = EncodedLength(source[0]);
        if (source.Length < size)
        {
            return CborDecodeResult.NeedMoreData;
        }

        argument = additional switch
        {
            CborEncoding.UInt8Argument => source[CborEncoding.ArgumentOffset],
            CborEncoding.UInt16Argument => BinaryPrimitives.ReadUInt16BigEndian(source.Slice(CborEncoding.ArgumentOffset)),
            CborEncoding.UInt32Argument => BinaryPrimitives.ReadUInt32BigEndian(source.Slice(CborEncoding.ArgumentOffset)),
            _ => BinaryPrimitives.ReadUInt64BigEndian(source.Slice(CborEncoding.ArgumentOffset)),
        };
        length = size;
        return CborDecodeResult.Success;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool TryReadContiguousArgument(ReadOnlySpan<byte> source, byte prefix, out ulong argument, out int length)
    {
        argument = 0;
        length = 0;
        if (source.IsEmpty)
        {
            return false;
        }

        int additional = source[0] ^ prefix;
        if (additional < CborEncoding.DirectArgumentLimit)
        {
            argument = (uint)additional;
            length = CborEncoding.ImmediateHeaderLength;
            return true;
        }

        switch (additional)
        {
            case CborEncoding.UInt8Argument when source.Length >= CborEncoding.UInt8HeaderLength:
                argument = source[CborEncoding.ArgumentOffset];
                length = CborEncoding.UInt8HeaderLength;
                return true;
            case CborEncoding.UInt16Argument when source.Length >= CborEncoding.UInt16HeaderLength:
                argument = BinaryPrimitives.ReadUInt16BigEndian(source.Slice(CborEncoding.ArgumentOffset));
                length = CborEncoding.UInt16HeaderLength;
                return true;
            case CborEncoding.UInt32Argument when source.Length >= CborEncoding.UInt32HeaderLength:
                argument = BinaryPrimitives.ReadUInt32BigEndian(source.Slice(CborEncoding.ArgumentOffset));
                length = CborEncoding.UInt32HeaderLength;
                return true;
            case CborEncoding.UInt64Argument when source.Length >= CborEncoding.UInt64HeaderLength:
                argument = BinaryPrimitives.ReadUInt64BigEndian(source.Slice(CborEncoding.ArgumentOffset));
                length = CborEncoding.UInt64HeaderLength;
                return true;
            default:
                return false;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static CborDecodeResult ReadFloat(ReadOnlySpan<byte> source, out double value, out int length)
    {
        value = 0;
        length = 0;
        if (source.IsEmpty)
        {
            return CborDecodeResult.NeedMoreData;
        }

        byte initial = source[0];
        int size;
        switch (initial)
        {
            case CborEncoding.Float16: size = CborEncoding.Float16Length; break;
            case CborEncoding.Float32: size = CborEncoding.Float32Length; break;
            case CborEncoding.Float64: size = CborEncoding.Float64Length; break;
            default: return Mismatch(source);
        }

        if (source.Length < size)
        {
            return CborDecodeResult.NeedMoreData;
        }

        value = initial switch
        {
            CborEncoding.Float16 => FloatEncoding.HalfFromBits(BinaryPrimitives.ReadUInt16BigEndian(source.Slice(CborEncoding.ArgumentOffset))),
            CborEncoding.Float32 => FloatEncoding.SingleFromBits(BinaryPrimitives.ReadUInt32BigEndian(source.Slice(CborEncoding.ArgumentOffset))),
            _ => BitConverter.Int64BitsToDouble((long)BinaryPrimitives.ReadUInt64BigEndian(source.Slice(CborEncoding.ArgumentOffset))),
        };
        length = size;
        return CborDecodeResult.Success;
    }

    // Structural prefix validation for a custom formatter, without decoding its argument twice.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static CborDecodeResult ValidateExtendedPrefix(ReadOnlySpan<byte> source)
    {
        byte initial = source[0];
        int additional = initial & CborEncoding.AdditionalInformationMask;
        if (additional is >= CborEncoding.FirstReservedArgument and <= CborEncoding.LastReservedArgument ||
            (additional == CborEncoding.IndefiniteArgument && (initial < CborEncoding.ByteStringPrefix || initial >= CborEncoding.TagPrefix)))
        {
            return CborDecodeResult.InvalidData;
        }

        int length = EncodedLength(initial);
        if (source.Length < length)
        {
            return CborDecodeResult.NeedMoreData;
        }

        return initial == CborEncoding.ExtendedSimple && source[CborEncoding.ArgumentOffset] < CborEncoding.MinimumExtendedSimpleValue ? CborDecodeResult.InvalidData : CborDecodeResult.Success;
    }
}
