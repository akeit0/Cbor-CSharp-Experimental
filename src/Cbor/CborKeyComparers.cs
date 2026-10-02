using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Cbor.Internal;

namespace Cbor;

/// <summary>Process-keyed SipHash comparers for supported CBOR dictionary key types.</summary>
/// <remarks>Unknown CLR key types require an explicit comparer. Keys retain CLR equality:
/// ordinal strings, equivalent signed zeros, and equivalent NaN representations.</remarks>
public static class CborKeyComparers
{
    private static readonly ulong Key0;
    private static readonly ulong Key1;

    static CborKeyComparers()
    {
        var key = new byte[16];
        using (var random = RandomNumberGenerator.Create())
        {
            random.GetBytes(key);
        }

        Key0 = BinaryPrimitives.ReadUInt64LittleEndian(key);
        Key1 = BinaryPrimitives.ReadUInt64LittleEndian(key.AsSpan(8));
    }

    /// <summary>Gets a comparer for integral, enum, Boolean, floating-point, or string keys.</summary>
    public static IEqualityComparer<T> Get<T>() => Cache<T>.Comparer ??
        throw new NotSupportedException("CBOR dictionary keys of type " + typeof(T).FullName + " need an explicit hash-flooding-resistant comparer.");

    private static int Hash(ReadOnlySpan<byte> source)
    {
        ulong hash = SipHash24.Hash(source, Key0, Key1);
        return (int)(hash ^ (hash >> 32));
    }

    private static class Cache<T>
    {
        internal static readonly IEqualityComparer<T>? Comparer = Create();

        private static IEqualityComparer<T>? Create()
        {
            Type type = typeof(T);
            if (type == typeof(string))
            {
                return (IEqualityComparer<T>)(object)new StringKeyComparer();
            }

            if (type == typeof(float))
            {
                return (IEqualityComparer<T>)(object)new SingleKeyComparer();
            }

            if (type == typeof(double))
            {
                return (IEqualityComparer<T>)(object)new DoubleKeyComparer();
            }

            if (type == typeof(bool) || type == typeof(byte) || type == typeof(sbyte) ||
                type == typeof(short) || type == typeof(ushort) || type == typeof(int) ||
                type == typeof(uint) || type == typeof(long) || type == typeof(ulong) || type.IsEnum)
            {
                return new BitwiseKeyComparer<T>();
            }

            return null;
        }
    }

    private sealed class StringKeyComparer : IEqualityComparer<string>
    {
        public bool Equals(string? x, string? y) => string.Equals(x, y, StringComparison.Ordinal);
        public int GetHashCode(string value) => Hash(MemoryMarshal.AsBytes(value.AsSpan()));
    }

    private sealed class BitwiseKeyComparer<T> : IEqualityComparer<T>
    {
        public bool Equals(T? x, T? y) => EqualityComparer<T>.Default.Equals(x!, y!);

        public int GetHashCode(T value)
        {
            // Only explicitly checked primitive/enum types reach this code; never hash struct padding.
            int size = Unsafe.SizeOf<T>();
            Span<byte> bytes = stackalloc byte[8];
            ref byte first = ref Unsafe.As<T, byte>(ref value);
            for (int i = 0; i < size; i++)
            {
                bytes[i] = Unsafe.Add(ref first, i);
            }

            return Hash(bytes.Slice(0, size));
        }
    }

    private sealed class SingleKeyComparer : IEqualityComparer<float>
    {
        public bool Equals(float x, float y) => x.Equals(y);

        public int GetHashCode(float value)
        {
            uint bits = value == 0 ? 0 : float.IsNaN(value) ? 0x7fc00000U : FloatEncoding.SingleToBits(value);
            Span<byte> bytes = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(bytes, bits);
            return Hash(bytes);
        }
    }

    private sealed class DoubleKeyComparer : IEqualityComparer<double>
    {
        public bool Equals(double x, double y) => x.Equals(y);

        public int GetHashCode(double value)
        {
            ulong bits = value == 0 ? 0 : double.IsNaN(value) ? 0x7ff8000000000000UL : (ulong)BitConverter.DoubleToInt64Bits(value);
            Span<byte> bytes = stackalloc byte[8];
            BinaryPrimitives.WriteUInt64LittleEndian(bytes, bits);
            return Hash(bytes);
        }
    }
}
