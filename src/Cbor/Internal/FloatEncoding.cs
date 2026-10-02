using System.Runtime.CompilerServices;

namespace Cbor.Internal;

internal static class FloatEncoding
{
    internal static int GetPreferredWidth(double value, out ulong bits)
    {
        if (double.IsNaN(value))
        {
            bits = CborEncoding.CanonicalHalfNaN;
            return CborEncoding.Float16Length;
        }

        if (TryHalfExactly(value, out ushort half))
        {
            bits = half;
            return CborEncoding.Float16Length;
        }

        float single = (float)value;
        if ((double)single == value)
        {
            bits = SingleToBits(single);
            return CborEncoding.Float32Length;
        }

        bits = (ulong)BitConverter.DoubleToInt64Bits(value);
        return CborEncoding.Float64Length;
    }

    internal static uint SingleToBits(float value)
    {
#if NET9_0_OR_GREATER
        return Unsafe.BitCast<float, uint>(value);
#else
        return Unsafe.As<float, uint>(ref value);
#endif
    }

    internal static float SingleFromBits(uint bits)
    {
#if NET9_0_OR_GREATER
        return Unsafe.BitCast<uint, float>(bits);
#else
        return Unsafe.As<uint, float>(ref bits);
#endif
    }

    internal static double HalfFromBits(ushort bits)
    {
        ulong sign = (ulong)(bits & 0x8000) << 48;
        int exponent = (bits >> 10) & 31;
        int fraction = bits & 1023;
        if (exponent == 31)
        {
            return BitConverter.Int64BitsToDouble((long)(sign | 0x7ff0000000000000UL | ((ulong)fraction << 42)));
        }

        if (exponent == 0)
        {
            if (fraction == 0)
            {
                return BitConverter.Int64BitsToDouble((long)sign);
            }

            exponent = 1;
            while ((fraction & 1024) == 0)
            {
                fraction <<= 1;
                exponent--;
            }

            fraction &= 1023;
        }

        ulong doubleExponent = (ulong)(exponent - 15 + 1023);
        return BitConverter.Int64BitsToDouble((long)(sign | (doubleExponent << 52) | ((ulong)fraction << 42)));
    }

    internal static bool TryHalfExactly(double value, out ushort bits)
    {
        float single = (float)value;
        bits = 0;
        if ((double)single != value)
        {
            return false; // Includes NaN; callers handle it explicitly.
        }

        uint raw = SingleToBits(single);
        ushort sign = (ushort)((raw >> 16) & 0x8000);
        int exponent = (int)((raw >> 23) & 255) - 127;
        uint fraction = raw & 0x7fffff;
        if ((raw & 0x7fffffff) == 0)
        {
            bits = sign; // Preserve negative zero.
            return true;
        }

        if (float.IsInfinity(single))
        {
            bits = (ushort)(sign | 0x7c00);
            return true;
        }

        if (exponent > 15 || exponent < -24)
        {
            return false;
        }

        if (exponent >= -14)
        {
            if ((fraction & 0x1fff) != 0)
            {
                return false;
            }

            bits = (ushort)((uint)sign | ((uint)(exponent + 15) << 10) | (fraction >> 13));
            return true;
        }

        int shift = -exponent - 1;
        uint significand = fraction | 0x800000;
        if ((significand & ((1u << shift) - 1)) != 0)
        {
            return false;
        }

        bits = (ushort)(sign | (significand >> shift));
        return true;
    }

    internal static bool IsPreferred(in CborHeader header)
    {
        double value = Decode(in header);
        if (double.IsNaN(value))
        {
            return header.AdditionalInformation == CborEncoding.Float16Argument && header.Argument == CborEncoding.CanonicalHalfNaN;
        }

        if (header.AdditionalInformation == CborEncoding.Float16Argument)
        {
            return true;
        }

        if (TryHalfExactly(value, out _))
        {
            return false;
        }

        return header.AdditionalInformation == CborEncoding.Float32Argument || (double)(float)value != value;
    }

    internal static double Decode(in CborHeader header) => header.AdditionalInformation switch
    {
        CborEncoding.Float16Argument => HalfFromBits((ushort)header.Argument),
        CborEncoding.Float32Argument => SingleFromBits((uint)header.Argument),
        _ => BitConverter.Int64BitsToDouble((long)header.Argument),
    };
}
