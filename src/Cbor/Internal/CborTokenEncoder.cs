using System.Buffers.Binary;
using System.Runtime.CompilerServices;
#if NET9_0_OR_GREATER
using System.Numerics;
using System.Runtime.InteropServices;
#endif

namespace Cbor.Internal;

// Each helper documents its required contiguous window; callers establish it before taking the reference.
// Integer writes may touch scratch bytes after the encoded token, within that nine-byte window.
internal static class CborTokenEncoder
{
#if NET9_0_OR_GREATER
    private const ushort UInt8Format = CborEncoding.UInt8HeaderLength | (CborEncoding.UInt8Argument << CborEncoding.BitsPerByte);
    private const ushort UInt16Format = CborEncoding.UInt16HeaderLength | (CborEncoding.UInt16Argument << CborEncoding.BitsPerByte);
    private const ushort UInt32Format = CborEncoding.UInt32HeaderLength | (CborEncoding.UInt32Argument << CborEncoding.BitsPerByte);
    private const ushort UInt64Format = CborEncoding.UInt64HeaderLength | (CborEncoding.UInt64Argument << CborEncoding.BitsPerByte);

    // Significant bits 0..64 -> packed encoded length and additional-information byte.
    private static ReadOnlySpan<ushort> ArgumentFormats =>
    [
        UInt8Format, UInt8Format, UInt8Format, UInt8Format, UInt8Format, UInt8Format, UInt8Format, UInt8Format,
        UInt8Format, UInt16Format, UInt16Format, UInt16Format, UInt16Format, UInt16Format, UInt16Format, UInt16Format,
        UInt16Format, UInt32Format, UInt32Format, UInt32Format, UInt32Format, UInt32Format, UInt32Format, UInt32Format,
        UInt32Format, UInt32Format, UInt32Format, UInt32Format, UInt32Format, UInt32Format, UInt32Format, UInt32Format,
        UInt32Format, UInt64Format, UInt64Format, UInt64Format, UInt64Format, UInt64Format, UInt64Format, UInt64Format,
        UInt64Format, UInt64Format, UInt64Format, UInt64Format, UInt64Format, UInt64Format, UInt64Format, UInt64Format,
        UInt64Format, UInt64Format, UInt64Format, UInt64Format, UInt64Format, UInt64Format, UInt64Format, UInt64Format,
        UInt64Format, UInt64Format, UInt64Format, UInt64Format, UInt64Format, UInt64Format, UInt64Format, UInt64Format,
        UInt64Format,
    ];
#endif

    // Destination must contain at least nine bytes. Major type must be zero through six.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int UnsafeWriteHeader(ref byte destination, CborMajorType major, ulong argument)
    {
        byte prefix = (byte)((byte)major << CborEncoding.MajorTypeShift);
        if (argument < CborEncoding.DirectArgumentLimit)
        {
            destination = (byte)(prefix | (byte)argument);
            return CborEncoding.ImmediateHeaderLength;
        }

#if NET9_0_OR_GREATER
        int bits = CborEncoding.UInt64BitCount - BitOperations.LeadingZeroCount(argument);
        ushort entry = Unsafe.Add(ref MemoryMarshal.GetReference(ArgumentFormats), bits);
        int length = entry & byte.MaxValue;
        destination = (byte)(prefix | (entry >> CborEncoding.BitsPerByte));
#else
        int length = CborPrimitives.GetHeaderLength(argument);
        destination = (byte)(prefix | (length == CborEncoding.UInt8HeaderLength ? CborEncoding.UInt8Argument :
            length == CborEncoding.UInt16HeaderLength ? CborEncoding.UInt16Argument :
            length == CborEncoding.UInt32HeaderLength ? CborEncoding.UInt32Argument : CborEncoding.UInt64Argument));
#endif
        // length >= 2: the shift is 56, 48, 32, or 0, never a masked shift of 64.
        ulong payload = argument << ((CborEncoding.UInt64HeaderLength - length) * CborEncoding.BitsPerByte);
        Unsafe.WriteUnaligned(ref Unsafe.Add(ref destination, CborEncoding.ArgumentOffset), ToBigEndian(payload));
        return length;
    }

    // Destination must contain length bytes; length must be GetHeaderLength(argument).
    internal static void UnsafeWriteExactHeader(ref byte destination, CborMajorType major, ulong argument, int length)
    {
        byte prefix = (byte)((byte)major << CborEncoding.MajorTypeShift);
        switch (length)
        {
            case CborEncoding.ImmediateHeaderLength:
                destination = (byte)(prefix | (byte)argument);
                break;
            case CborEncoding.UInt8HeaderLength:
                destination = (byte)(prefix | CborEncoding.UInt8Argument);
                Unsafe.Add(ref destination, CborEncoding.ArgumentOffset) = (byte)argument;
                break;
            case CborEncoding.UInt16HeaderLength:
                destination = (byte)(prefix | CborEncoding.UInt16Argument);
                ushort shortValue = (ushort)argument;
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref destination, CborEncoding.ArgumentOffset),
                    BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(shortValue) : shortValue);
                break;
            case CborEncoding.UInt32HeaderLength:
                destination = (byte)(prefix | CborEncoding.UInt32Argument);
                uint intValue = (uint)argument;
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref destination, CborEncoding.ArgumentOffset),
                    BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(intValue) : intValue);
                break;
            default:
                destination = (byte)(prefix | CborEncoding.UInt64Argument);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref destination, CborEncoding.ArgumentOffset), ToBigEndian(argument));
                break;
        }
    }

    // Destination must contain at least three bytes.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void UnsafeWriteHalfBits(ref byte destination, ushort bits)
    {
        destination = CborEncoding.Float16;
        Unsafe.WriteUnaligned(ref Unsafe.Add(ref destination, CborEncoding.ArgumentOffset),
            BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(bits) : bits);
    }

    // Destination must contain at least five bytes.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void UnsafeWriteSingle(ref byte destination, float value)
    {
        destination = CborEncoding.Float32;
        uint bits = FloatEncoding.SingleToBits(value);
        Unsafe.WriteUnaligned(ref Unsafe.Add(ref destination, CborEncoding.ArgumentOffset),
            BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(bits) : bits);
    }

    // Destination must contain at least nine bytes.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void UnsafeWriteDoublePrecision(ref byte destination, double value)
    {
        destination = CborEncoding.Float64;
        Unsafe.WriteUnaligned(ref Unsafe.Add(ref destination, CborEncoding.ArgumentOffset), ToBigEndian((ulong)BitConverter.DoubleToInt64Bits(value)));
    }

    // Destination must contain length bytes. Length and bits come from GetPreferredWidth.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void UnsafeWriteFloatBits(ref byte destination, ulong bits, int length)
    {
        switch (length)
        {
            case CborEncoding.Float16Length:
                UnsafeWriteHalfBits(ref destination, (ushort)bits);
                break;
            case CborEncoding.Float32Length:
                destination = CborEncoding.Float32;
                uint singleBits = (uint)bits;
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref destination, CborEncoding.ArgumentOffset),
                    BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(singleBits) : singleBits);
                break;
            default:
                destination = CborEncoding.Float64;
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref destination, CborEncoding.ArgumentOffset), ToBigEndian(bits));
                break;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong ToBigEndian(ulong value) =>
        BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
}
