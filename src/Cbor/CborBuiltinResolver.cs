using Cbor.Internal;

namespace Cbor;

/// <summary>AOT-safe scalar and string formatters. Collection closures are explicitly registered or generated.</summary>
public sealed class CborBuiltinResolver : CborFormatterResolver
{
    /// <summary>Shared, immutable resolver.</summary>
    public static CborBuiltinResolver Instance { get; } = new();

    private CborBuiltinResolver()
    {
    }

    /// <inheritdoc />
    public override ICborFormatter<T>? GetFormatter<T>() => Cache<T>.Formatter;

    private static class Cache<T>
    {
        internal static readonly ICborFormatter<T>? Formatter = Create();

        private static ICborFormatter<T>? Create()
        {
            if (typeof(T) == typeof(bool))
            {
                return (ICborFormatter<T>)(object)new BooleanFormatter();
            }

            if (typeof(T) == typeof(byte))
            {
                return (ICborFormatter<T>)(object)new ByteFormatter();
            }

            if (typeof(T) == typeof(sbyte))
            {
                return (ICborFormatter<T>)(object)new SByteFormatter();
            }

            if (typeof(T) == typeof(short))
            {
                return (ICborFormatter<T>)(object)new Int16Formatter();
            }

            if (typeof(T) == typeof(ushort))
            {
                return (ICborFormatter<T>)(object)new UInt16Formatter();
            }

            if (typeof(T) == typeof(int))
            {
                return (ICborFormatter<T>)(object)new Int32Formatter();
            }

            if (typeof(T) == typeof(uint))
            {
                return (ICborFormatter<T>)(object)new UInt32Formatter();
            }

            if (typeof(T) == typeof(long))
            {
                return (ICborFormatter<T>)(object)new Int64Formatter();
            }

            if (typeof(T) == typeof(ulong))
            {
                return (ICborFormatter<T>)(object)new UInt64Formatter();
            }

            if (typeof(T) == typeof(double))
            {
                return (ICborFormatter<T>)(object)new DoubleFormatter();
            }

            if (typeof(T) == typeof(float))
            {
                return (ICborFormatter<T>)(object)new SingleFormatter();
            }

            if (typeof(T) == typeof(string))
            {
                return (ICborFormatter<T>)(object)new StringFormatter();
            }

            if (typeof(T) == typeof(byte[]))
            {
                return (ICborFormatter<T>)(object)new ByteStringFormatter();
            }

            return null;
        }
    }
}
