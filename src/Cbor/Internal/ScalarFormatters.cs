using SerializerFoundation;

namespace Cbor.Internal;

internal sealed class BooleanFormatter<TWriteBuffer, TReadBuffer> :
    ICborFormatter<TWriteBuffer, TReadBuffer, bool>
    where TWriteBuffer : struct, IWriteBuffer
#if NET9_0_OR_GREATER
    , allows ref struct
#endif
    where TReadBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
    , allows ref struct
#endif
{
    /// <inheritdoc />
    public void Initialize(CborFormatterResolver resolver) { }
    public void Serialize(ref TWriteBuffer buffer, ref CborSerializationContext context, bool value)
        => buffer.WriteBoolean(value);
    public bool Deserialize(ref TReadBuffer buffer, ref CborDeserializationContext context)
        => buffer.ReadBoolean();
}

internal sealed class ByteFormatter<TWriteBuffer, TReadBuffer> :
    ICborFormatter<TWriteBuffer, TReadBuffer, byte>
    where TWriteBuffer : struct, IWriteBuffer
#if NET9_0_OR_GREATER
    , allows ref struct
#endif
    where TReadBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
    , allows ref struct
#endif
{
    /// <inheritdoc />
    public void Initialize(CborFormatterResolver resolver) { }
    public void Serialize(ref TWriteBuffer buffer, ref CborSerializationContext context, byte value)
        => buffer.WriteUInt64(value);
    public byte Deserialize(ref TReadBuffer buffer, ref CborDeserializationContext context)
        => checked((byte)buffer.ReadUInt64());
}

internal sealed class SByteFormatter<TWriteBuffer, TReadBuffer> :
    ICborFormatter<TWriteBuffer, TReadBuffer, sbyte>
    where TWriteBuffer : struct, IWriteBuffer
#if NET9_0_OR_GREATER
    , allows ref struct
#endif
    where TReadBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
    , allows ref struct
#endif
{
    /// <inheritdoc />
    public void Initialize(CborFormatterResolver resolver) { }
    public void Serialize(ref TWriteBuffer buffer, ref CborSerializationContext context, sbyte value)
        => buffer.WriteInt64(value);
    public sbyte Deserialize(ref TReadBuffer buffer, ref CborDeserializationContext context)
        => checked((sbyte)buffer.ReadInt64());
}

internal sealed class Int16Formatter<TWriteBuffer, TReadBuffer> :
    ICborFormatter<TWriteBuffer, TReadBuffer, short>
    where TWriteBuffer : struct, IWriteBuffer
#if NET9_0_OR_GREATER
    , allows ref struct
#endif
    where TReadBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
    , allows ref struct
#endif
{
    /// <inheritdoc />
    public void Initialize(CborFormatterResolver resolver) { }
    public void Serialize(ref TWriteBuffer buffer, ref CborSerializationContext context, short value)
        => buffer.WriteInt64(value);
    public short Deserialize(ref TReadBuffer buffer, ref CborDeserializationContext context)
        => checked((short)buffer.ReadInt64());
}

internal sealed class UInt16Formatter<TWriteBuffer, TReadBuffer> :
    ICborFormatter<TWriteBuffer, TReadBuffer, ushort>
    where TWriteBuffer : struct, IWriteBuffer
#if NET9_0_OR_GREATER
    , allows ref struct
#endif
    where TReadBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
    , allows ref struct
#endif
{
    /// <inheritdoc />
    public void Initialize(CborFormatterResolver resolver) { }
    public void Serialize(ref TWriteBuffer buffer, ref CborSerializationContext context, ushort value)
        => buffer.WriteUInt64(value);
    public ushort Deserialize(ref TReadBuffer buffer, ref CborDeserializationContext context)
        => checked((ushort)buffer.ReadUInt64());
}

internal sealed class Int32Formatter<TWriteBuffer, TReadBuffer> :
    ICborFormatter<TWriteBuffer, TReadBuffer, int>
    where TWriteBuffer : struct, IWriteBuffer
#if NET9_0_OR_GREATER
    , allows ref struct
#endif
    where TReadBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
    , allows ref struct
#endif
{
    /// <inheritdoc />
    public void Initialize(CborFormatterResolver resolver) { }
    public void Serialize(ref TWriteBuffer buffer, ref CborSerializationContext context, int value)
        => buffer.WriteInt64(value);
    public int Deserialize(ref TReadBuffer buffer, ref CborDeserializationContext context)
        => checked((int)buffer.ReadInt64());
}

internal sealed class UInt32Formatter<TWriteBuffer, TReadBuffer> :
    ICborFormatter<TWriteBuffer, TReadBuffer, uint>
    where TWriteBuffer : struct, IWriteBuffer
#if NET9_0_OR_GREATER
    , allows ref struct
#endif
    where TReadBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
    , allows ref struct
#endif
{
    /// <inheritdoc />
    public void Initialize(CborFormatterResolver resolver) { }
    public void Serialize(ref TWriteBuffer buffer, ref CborSerializationContext context, uint value)
        => buffer.WriteUInt64(value);
    public uint Deserialize(ref TReadBuffer buffer, ref CborDeserializationContext context)
        => checked((uint)buffer.ReadUInt64());
}

internal sealed class Int64Formatter<TWriteBuffer, TReadBuffer> :
    ICborFormatter<TWriteBuffer, TReadBuffer, long>
    where TWriteBuffer : struct, IWriteBuffer
#if NET9_0_OR_GREATER
    , allows ref struct
#endif
    where TReadBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
    , allows ref struct
#endif
{
    /// <inheritdoc />
    public void Initialize(CborFormatterResolver resolver) { }
    public void Serialize(ref TWriteBuffer buffer, ref CborSerializationContext context, long value)
        => buffer.WriteInt64(value);
    public long Deserialize(ref TReadBuffer buffer, ref CborDeserializationContext context)
        => buffer.ReadInt64();
}

internal sealed class UInt64Formatter<TWriteBuffer, TReadBuffer> :
    ICborFormatter<TWriteBuffer, TReadBuffer, ulong>
    where TWriteBuffer : struct, IWriteBuffer
#if NET9_0_OR_GREATER
    , allows ref struct
#endif
    where TReadBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
    , allows ref struct
#endif
{
    /// <inheritdoc />
    public void Initialize(CborFormatterResolver resolver) { }
    public void Serialize(ref TWriteBuffer buffer, ref CborSerializationContext context, ulong value)
        => buffer.WriteUInt64(value);
    public ulong Deserialize(ref TReadBuffer buffer, ref CborDeserializationContext context)
        => buffer.ReadUInt64();
}

internal sealed class DoubleFormatter<TWriteBuffer, TReadBuffer> :
    ICborFormatter<TWriteBuffer, TReadBuffer, double>
    where TWriteBuffer : struct, IWriteBuffer
#if NET9_0_OR_GREATER
    , allows ref struct
#endif
    where TReadBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
    , allows ref struct
#endif
{
    /// <inheritdoc />
    public void Initialize(CborFormatterResolver resolver) { }
    public void Serialize(ref TWriteBuffer buffer, ref CborSerializationContext context, double value)
        => buffer.WriteDouble(value);
    public double Deserialize(ref TReadBuffer buffer, ref CborDeserializationContext context)
        => buffer.ReadDouble();
}

internal sealed class SingleFormatter<TWriteBuffer, TReadBuffer> :
    ICborFormatter<TWriteBuffer, TReadBuffer, float>
    where TWriteBuffer : struct, IWriteBuffer
#if NET9_0_OR_GREATER
    , allows ref struct
#endif
    where TReadBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
    , allows ref struct
#endif
{
    /// <inheritdoc />
    public void Initialize(CborFormatterResolver resolver) { }
    public void Serialize(ref TWriteBuffer buffer, ref CborSerializationContext context, float value)
        => buffer.WriteDouble(value);
    public float Deserialize(ref TReadBuffer buffer, ref CborDeserializationContext context)
    {
        double value = buffer.ReadDouble();
        if (!double.IsInfinity(value) && (value > float.MaxValue || value < -float.MaxValue))
        {
            throw new OverflowException("The CBOR floating-point value exceeds Single's finite range.");
        }

        return (float)value;
    }
}
