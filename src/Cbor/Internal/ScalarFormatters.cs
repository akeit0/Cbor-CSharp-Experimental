using SerializerFoundation;

namespace Cbor.Internal;

internal sealed class BooleanFormatter : ICborFormatter<bool>
{
    public void Serialize<TWriteBuffer>(ref TWriteBuffer buffer, ref CborSerializationContext context, bool value)
        where TWriteBuffer : struct, IWriteBuffer
#if NET9_0_OR_GREATER
            , allows ref struct
#endif
        => buffer.WriteBoolean(value);

    public bool Deserialize<TReadBuffer>(ref TReadBuffer buffer, ref CborDeserializationContext context)
        where TReadBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
            , allows ref struct
#endif
        => buffer.ReadBoolean();
}

internal sealed class ByteFormatter : ICborFormatter<byte>
{
    public void Serialize<TWriteBuffer>(ref TWriteBuffer buffer, ref CborSerializationContext context, byte value)
        where TWriteBuffer : struct, IWriteBuffer
#if NET9_0_OR_GREATER
            , allows ref struct
#endif
        => buffer.WriteUInt64(value);

    public byte Deserialize<TReadBuffer>(ref TReadBuffer buffer, ref CborDeserializationContext context)
        where TReadBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
            , allows ref struct
#endif
        => checked((byte)buffer.ReadUInt64());
}

internal sealed class SByteFormatter : ICborFormatter<sbyte>
{
    public void Serialize<TWriteBuffer>(ref TWriteBuffer buffer, ref CborSerializationContext context, sbyte value)
        where TWriteBuffer : struct, IWriteBuffer
#if NET9_0_OR_GREATER
            , allows ref struct
#endif
        => buffer.WriteInt64(value);

    public sbyte Deserialize<TReadBuffer>(ref TReadBuffer buffer, ref CborDeserializationContext context)
        where TReadBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
            , allows ref struct
#endif
        => checked((sbyte)buffer.ReadInt64());
}

internal sealed class Int16Formatter : ICborFormatter<short>
{
    public void Serialize<TWriteBuffer>(ref TWriteBuffer buffer, ref CborSerializationContext context, short value)
        where TWriteBuffer : struct, IWriteBuffer
#if NET9_0_OR_GREATER
            , allows ref struct
#endif
        => buffer.WriteInt64(value);

    public short Deserialize<TReadBuffer>(ref TReadBuffer buffer, ref CborDeserializationContext context)
        where TReadBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
            , allows ref struct
#endif
        => checked((short)buffer.ReadInt64());
}

internal sealed class UInt16Formatter : ICborFormatter<ushort>
{
    public void Serialize<TWriteBuffer>(ref TWriteBuffer buffer, ref CborSerializationContext context, ushort value)
        where TWriteBuffer : struct, IWriteBuffer
#if NET9_0_OR_GREATER
            , allows ref struct
#endif
        => buffer.WriteUInt64(value);

    public ushort Deserialize<TReadBuffer>(ref TReadBuffer buffer, ref CborDeserializationContext context)
        where TReadBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
            , allows ref struct
#endif
        => checked((ushort)buffer.ReadUInt64());
}

internal sealed class Int32Formatter : ICborFormatter<int>
{
    public void Serialize<TWriteBuffer>(ref TWriteBuffer buffer, ref CborSerializationContext context, int value)
        where TWriteBuffer : struct, IWriteBuffer
#if NET9_0_OR_GREATER
            , allows ref struct
#endif
        => buffer.WriteInt64(value);

    public int Deserialize<TReadBuffer>(ref TReadBuffer buffer, ref CborDeserializationContext context)
        where TReadBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
            , allows ref struct
#endif
        => checked((int)buffer.ReadInt64());
}

internal sealed class UInt32Formatter : ICborFormatter<uint>
{
    public void Serialize<TWriteBuffer>(ref TWriteBuffer buffer, ref CborSerializationContext context, uint value)
        where TWriteBuffer : struct, IWriteBuffer
#if NET9_0_OR_GREATER
            , allows ref struct
#endif
        => buffer.WriteUInt64(value);

    public uint Deserialize<TReadBuffer>(ref TReadBuffer buffer, ref CborDeserializationContext context)
        where TReadBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
            , allows ref struct
#endif
        => checked((uint)buffer.ReadUInt64());
}

internal sealed class Int64Formatter : ICborFormatter<long>
{
    public void Serialize<TWriteBuffer>(ref TWriteBuffer buffer, ref CborSerializationContext context, long value)
        where TWriteBuffer : struct, IWriteBuffer
#if NET9_0_OR_GREATER
            , allows ref struct
#endif
        => buffer.WriteInt64(value);

    public long Deserialize<TReadBuffer>(ref TReadBuffer buffer, ref CborDeserializationContext context)
        where TReadBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
            , allows ref struct
#endif
        => buffer.ReadInt64();
}

internal sealed class UInt64Formatter : ICborFormatter<ulong>
{
    public void Serialize<TWriteBuffer>(ref TWriteBuffer buffer, ref CborSerializationContext context, ulong value)
        where TWriteBuffer : struct, IWriteBuffer
#if NET9_0_OR_GREATER
            , allows ref struct
#endif
        => buffer.WriteUInt64(value);

    public ulong Deserialize<TReadBuffer>(ref TReadBuffer buffer, ref CborDeserializationContext context)
        where TReadBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
            , allows ref struct
#endif
        => buffer.ReadUInt64();
}

internal sealed class DoubleFormatter : ICborFormatter<double>
{
    public void Serialize<TWriteBuffer>(ref TWriteBuffer buffer, ref CborSerializationContext context, double value)
        where TWriteBuffer : struct, IWriteBuffer
#if NET9_0_OR_GREATER
            , allows ref struct
#endif
        => buffer.WriteDouble(value);

    public double Deserialize<TReadBuffer>(ref TReadBuffer buffer, ref CborDeserializationContext context)
        where TReadBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
            , allows ref struct
#endif
        => buffer.ReadDouble();
}

internal sealed class SingleFormatter : ICborFormatter<float>
{
    public void Serialize<TWriteBuffer>(ref TWriteBuffer buffer, ref CborSerializationContext context, float value)
        where TWriteBuffer : struct, IWriteBuffer
#if NET9_0_OR_GREATER
            , allows ref struct
#endif
        => buffer.WriteDouble(value);

    public float Deserialize<TReadBuffer>(ref TReadBuffer buffer, ref CborDeserializationContext context)
        where TReadBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
            , allows ref struct
#endif
    {
        double value = buffer.ReadDouble();
        float result = (float)value;
        if (float.IsInfinity(result) && !double.IsInfinity(value))
        {
            throw new OverflowException("The CBOR floating-point value exceeds Single's finite range.");
        }

        return result;
    }
}
