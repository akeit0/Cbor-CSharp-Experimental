using Cbor;
using SerializerFoundation;

namespace LegacyPackageProvider;

/// <summary>A graph generated for netstandard2.0, loaded by the modern package consumer.</summary>
[CborObject]
public sealed class LegacyNode
{
    /// <summary>Numeric content subject to the caller's override.</summary>
    [CborKey(0)]
    public int Value { get; set; }

    /// <summary>Recursive child contract.</summary>
    [CborKey(1)]
    public LegacyNode? Child { get; set; }
}

/// <summary>The downlevel factory must serve compatible pairs through its stable Type overload.</summary>
[CborResolver(typeof(LegacyNode))]
public partial class LegacyResolver;

/// <summary>A formatter compiled against the original single-type ordinary-buffer contract.</summary>
public sealed class LegacyOffsetFormatter : ICborFormatter<int>
{
    /// <inheritdoc />
    public void Serialize<TWriteBuffer>(ref TWriteBuffer buffer, ref CborSerializationContext context, int value)
        where TWriteBuffer : struct, IWriteBuffer => buffer.WriteInt64(value + 1);

    /// <inheritdoc />
    public int Deserialize<TReadBuffer>(ref TReadBuffer buffer, ref CborDeserializationContext context)
        where TReadBuffer : struct, IReadBuffer => checked((int)buffer.ReadInt64() - 1);
}

/// <summary>Exercises v4-style Type dispatch from a downlevel factory ahead of modern built-ins.</summary>
public sealed class LegacyIntegerFactory : CborFormatterFactory
{
    /// <inheritdoc />
    public override object? CreateFormatter(Type writeBufferType, Type readBufferType, Type valueType)
    {
        if (valueType != typeof(int)) { return null; }
        if (writeBufferType == typeof(CompatibleArrayPoolListWriteBuffer) && readBufferType == typeof(CompatibleReadOnlySpanReadBuffer))
        { return new IntegerFormatter<CompatibleArrayPoolListWriteBuffer, CompatibleReadOnlySpanReadBuffer>(); }
        if (writeBufferType == typeof(CompatibleArrayPoolListWriteBuffer) && readBufferType == typeof(CompatibleReadOnlySequenceReadBuffer))
        { return new IntegerFormatter<CompatibleArrayPoolListWriteBuffer, CompatibleReadOnlySequenceReadBuffer>(); }
        if (writeBufferType == typeof(CompatibleBufferWriterWriteBuffer) && readBufferType == typeof(CompatibleReadOnlySpanReadBuffer))
        { return new IntegerFormatter<CompatibleBufferWriterWriteBuffer, CompatibleReadOnlySpanReadBuffer>(); }
        if (writeBufferType == typeof(CompatibleBufferWriterWriteBuffer) && readBufferType == typeof(CompatibleReadOnlySequenceReadBuffer))
        { return new IntegerFormatter<CompatibleBufferWriterWriteBuffer, CompatibleReadOnlySequenceReadBuffer>(); }
        return null;
    }
    private sealed class IntegerFormatter<TWriteBuffer, TReadBuffer> : ICborFormatter<TWriteBuffer, TReadBuffer, int>
        where TWriteBuffer : struct, IWriteBuffer
        where TReadBuffer : struct, IReadBuffer
    {
        public void Initialize(CborFormatterResolver resolver) { }
        public void Serialize(ref TWriteBuffer buffer, ref CborSerializationContext context, int value) => buffer.WriteInt64(value + 5);
        public int Deserialize(ref TReadBuffer buffer, ref CborDeserializationContext context) => checked((int)buffer.ReadInt64() - 5);
    }
}
