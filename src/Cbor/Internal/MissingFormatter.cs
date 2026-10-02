using SerializerFoundation;

namespace Cbor.Internal;

internal sealed class MissingFormatter<TWriteBuffer, TReadBuffer, T> : ICborFormatter<TWriteBuffer, TReadBuffer, T>
    where TWriteBuffer : struct, IWriteBuffer
#if NET9_0_OR_GREATER
    , allows ref struct
#endif
    where TReadBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
    , allows ref struct
#endif
{
    public void Initialize(CborFormatterResolver resolver) { }
    public void Serialize(ref TWriteBuffer buffer, ref CborSerializationContext context, T value) => throw Missing();
    public T Deserialize(ref TReadBuffer buffer, ref CborDeserializationContext context) => throw Missing();
    private static NotSupportedException Missing() => new("No CBOR formatter is registered for " + typeof(T).FullName + " over the requested buffer pair.");
}
