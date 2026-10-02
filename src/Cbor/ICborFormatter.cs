using SerializerFoundation;

namespace Cbor;

/// <summary>Converts one CBOR item using a fixed write/read buffer pair. The resolver initializes each instance once before publication.</summary>
/// <remarks>Initialize only acquires dependencies; recursive children may still be initializing.
/// After initialization the formatter must be immutable and thread-safe. Buffers and contexts are borrowed by reference and never disposed here.</remarks>
/// <typeparam name="TWriteBuffer">The write buffer.</typeparam>
/// <typeparam name="TReadBuffer">The read buffer.</typeparam>
/// <typeparam name="T">The CLR value type.</typeparam>
public interface ICborFormatter<TWriteBuffer, TReadBuffer, T>
    where TWriteBuffer : struct, IWriteBuffer
#if NET9_0_OR_GREATER
    , allows ref struct
#endif
    where TReadBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
    , allows ref struct
#endif
{
    /// <summary>Acquires child formatters from this resolver. Called once under its construction lock.</summary>
    void Initialize(CborFormatterResolver resolver);

    /// <summary>Writes exactly one item. Nested values must use the context and cached child formatters.</summary>
    void Serialize(ref TWriteBuffer buffer, ref CborSerializationContext context, T value);

    /// <summary>Reads exactly one item. Nested values must use the context and cached child formatters.</summary>
    T Deserialize(ref TReadBuffer buffer, ref CborDeserializationContext context);
}
