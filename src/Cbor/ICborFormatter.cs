using SerializerFoundation;

namespace Cbor;

/// <summary>Stateless, thread-safe conversion of a CLR type to and from one CBOR item.</summary>
/// <remarks>Nested values must go through the context. Containers must pair EnterContainer and
/// ExitContainer in a finally block. Preflight known-length payloads with CheckEncodedLength before
/// requesting memory. Buffers are borrowed by reference and never disposed here.</remarks>
/// <typeparam name="T">The CLR value type.</typeparam>
public interface ICborFormatter<T>
{
    /// <summary>Writes exactly one item.</summary>
    void Serialize<TWriteBuffer>(ref TWriteBuffer buffer, ref CborSerializationContext context, T value)
        where TWriteBuffer : struct, IWriteBuffer
#if NET9_0_OR_GREATER
            , allows ref struct
#endif
        ;

    /// <summary>Reads exactly one item.</summary>
    T Deserialize<TReadBuffer>(ref TReadBuffer buffer, ref CborDeserializationContext context)
        where TReadBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
            , allows ref struct
#endif
        ;
}
