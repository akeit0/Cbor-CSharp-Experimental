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
    ICborFormatter<TWriteBuffer, TReadBuffer, byte>, IIntegerArrayWriter<TWriteBuffer, byte>
    where TWriteBuffer : struct, IWriteBuffer
#if NET9_0_OR_GREATER
    , allows ref struct
#endif
    where TReadBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
    , allows ref struct
#endif
{
    void IIntegerArrayWriter<TWriteBuffer, byte>.WriteElements(ref TWriteBuffer buffer, ref CborSerializationContext context, ReadOnlySpan<byte> source)
        => IntegerArrayWriter.WriteElements<TWriteBuffer, TReadBuffer, byte, ByteElementWriter>(ref buffer, ref context, source, this);

    /// <inheritdoc />
    public void Initialize(CborFormatterResolver resolver) { }
    public void Serialize(ref TWriteBuffer buffer, ref CborSerializationContext context, byte value)
        => buffer.WriteUInt64(value);
    public byte Deserialize(ref TReadBuffer buffer, ref CborDeserializationContext context)
        => checked((byte)buffer.ReadUInt64());
}

internal sealed class SByteFormatter<TWriteBuffer, TReadBuffer> :
    ICborFormatter<TWriteBuffer, TReadBuffer, sbyte>, IIntegerArrayWriter<TWriteBuffer, sbyte>
    where TWriteBuffer : struct, IWriteBuffer
#if NET9_0_OR_GREATER
    , allows ref struct
#endif
    where TReadBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
    , allows ref struct
#endif
{
    void IIntegerArrayWriter<TWriteBuffer, sbyte>.WriteElements(ref TWriteBuffer buffer, ref CborSerializationContext context, ReadOnlySpan<sbyte> source)
        => IntegerArrayWriter.WriteElements<TWriteBuffer, TReadBuffer, sbyte, SByteElementWriter>(ref buffer, ref context, source, this);

    /// <inheritdoc />
    public void Initialize(CborFormatterResolver resolver) { }
    public void Serialize(ref TWriteBuffer buffer, ref CborSerializationContext context, sbyte value)
        => buffer.WriteInt64(value);
    public sbyte Deserialize(ref TReadBuffer buffer, ref CborDeserializationContext context)
        => checked((sbyte)buffer.ReadInt64());
}

internal sealed class Int16Formatter<TWriteBuffer, TReadBuffer> :
    ICborFormatter<TWriteBuffer, TReadBuffer, short>, IIntegerArrayWriter<TWriteBuffer, short>
    where TWriteBuffer : struct, IWriteBuffer
#if NET9_0_OR_GREATER
    , allows ref struct
#endif
    where TReadBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
    , allows ref struct
#endif
{
    void IIntegerArrayWriter<TWriteBuffer, short>.WriteElements(ref TWriteBuffer buffer, ref CborSerializationContext context, ReadOnlySpan<short> source)
        => IntegerArrayWriter.WriteElements<TWriteBuffer, TReadBuffer, short, Int16ElementWriter>(ref buffer, ref context, source, this);

    /// <inheritdoc />
    public void Initialize(CborFormatterResolver resolver) { }
    public void Serialize(ref TWriteBuffer buffer, ref CborSerializationContext context, short value)
        => buffer.WriteInt64(value);
    public short Deserialize(ref TReadBuffer buffer, ref CborDeserializationContext context)
        => checked((short)buffer.ReadInt64());
}

internal sealed class UInt16Formatter<TWriteBuffer, TReadBuffer> :
    ICborFormatter<TWriteBuffer, TReadBuffer, ushort>, IIntegerArrayWriter<TWriteBuffer, ushort>
    where TWriteBuffer : struct, IWriteBuffer
#if NET9_0_OR_GREATER
    , allows ref struct
#endif
    where TReadBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
    , allows ref struct
#endif
{
    void IIntegerArrayWriter<TWriteBuffer, ushort>.WriteElements(ref TWriteBuffer buffer, ref CborSerializationContext context, ReadOnlySpan<ushort> source)
        => IntegerArrayWriter.WriteElements<TWriteBuffer, TReadBuffer, ushort, UInt16ElementWriter>(ref buffer, ref context, source, this);

    /// <inheritdoc />
    public void Initialize(CborFormatterResolver resolver) { }
    public void Serialize(ref TWriteBuffer buffer, ref CborSerializationContext context, ushort value)
        => buffer.WriteUInt64(value);
    public ushort Deserialize(ref TReadBuffer buffer, ref CborDeserializationContext context)
        => checked((ushort)buffer.ReadUInt64());
}

internal sealed class Int32Formatter<TWriteBuffer, TReadBuffer> :
    ICborFormatter<TWriteBuffer, TReadBuffer, int>, IIntegerArrayWriter<TWriteBuffer, int>
    where TWriteBuffer : struct, IWriteBuffer
#if NET9_0_OR_GREATER
    , allows ref struct
#endif
    where TReadBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
    , allows ref struct
#endif
{
    void IIntegerArrayWriter<TWriteBuffer, int>.WriteElements(ref TWriteBuffer buffer, ref CborSerializationContext context, ReadOnlySpan<int> source)
        => IntegerArrayWriter.WriteElements<TWriteBuffer, TReadBuffer, int, Int32ElementWriter>(ref buffer, ref context, source, this);

    /// <inheritdoc />
    public void Initialize(CborFormatterResolver resolver) { }
    public void Serialize(ref TWriteBuffer buffer, ref CborSerializationContext context, int value)
        => buffer.WriteInt64(value);
    public int Deserialize(ref TReadBuffer buffer, ref CborDeserializationContext context)
        => checked((int)buffer.ReadInt64());
}

internal sealed class UInt32Formatter<TWriteBuffer, TReadBuffer> :
    ICborFormatter<TWriteBuffer, TReadBuffer, uint>, IIntegerArrayWriter<TWriteBuffer, uint>
    where TWriteBuffer : struct, IWriteBuffer
#if NET9_0_OR_GREATER
    , allows ref struct
#endif
    where TReadBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
    , allows ref struct
#endif
{
    void IIntegerArrayWriter<TWriteBuffer, uint>.WriteElements(ref TWriteBuffer buffer, ref CborSerializationContext context, ReadOnlySpan<uint> source)
        => IntegerArrayWriter.WriteElements<TWriteBuffer, TReadBuffer, uint, UInt32ElementWriter>(ref buffer, ref context, source, this);

    /// <inheritdoc />
    public void Initialize(CborFormatterResolver resolver) { }
    public void Serialize(ref TWriteBuffer buffer, ref CborSerializationContext context, uint value)
        => buffer.WriteUInt64(value);
    public uint Deserialize(ref TReadBuffer buffer, ref CborDeserializationContext context)
        => checked((uint)buffer.ReadUInt64());
}

internal sealed class Int64Formatter<TWriteBuffer, TReadBuffer> :
    ICborFormatter<TWriteBuffer, TReadBuffer, long>, IIntegerArrayWriter<TWriteBuffer, long>
    where TWriteBuffer : struct, IWriteBuffer
#if NET9_0_OR_GREATER
    , allows ref struct
#endif
    where TReadBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
    , allows ref struct
#endif
{
    void IIntegerArrayWriter<TWriteBuffer, long>.WriteElements(ref TWriteBuffer buffer, ref CborSerializationContext context, ReadOnlySpan<long> source)
        => IntegerArrayWriter.WriteElements<TWriteBuffer, TReadBuffer, long, Int64ElementWriter>(ref buffer, ref context, source, this);

    /// <inheritdoc />
    public void Initialize(CborFormatterResolver resolver) { }
    public void Serialize(ref TWriteBuffer buffer, ref CborSerializationContext context, long value)
        => buffer.WriteInt64(value);
    public long Deserialize(ref TReadBuffer buffer, ref CborDeserializationContext context)
        => buffer.ReadInt64();
}

internal sealed class UInt64Formatter<TWriteBuffer, TReadBuffer> :
    ICborFormatter<TWriteBuffer, TReadBuffer, ulong>, IIntegerArrayWriter<TWriteBuffer, ulong>
    where TWriteBuffer : struct, IWriteBuffer
#if NET9_0_OR_GREATER
    , allows ref struct
#endif
    where TReadBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
    , allows ref struct
#endif
{
    void IIntegerArrayWriter<TWriteBuffer, ulong>.WriteElements(ref TWriteBuffer buffer, ref CborSerializationContext context, ReadOnlySpan<ulong> source)
        => IntegerArrayWriter.WriteElements<TWriteBuffer, TReadBuffer, ulong, UInt64ElementWriter>(ref buffer, ref context, source, this);

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
