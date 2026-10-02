using System.Buffers;
using SerializerFoundation;

namespace Cbor;

/// <summary>Typed CBOR serialization over spans, sequences, and caller-owned buffer writers.</summary>
/// <remarks>Deserialization consumes exactly one item and rejects trailing bytes. Formatter exceptions
/// propagate unchanged. Streaming writes may commit a prefix before an error; callers own rollback.</remarks>
public static class CborSerializer
{
    /// <summary>Serializes a value to an independently owned byte array.</summary>
    public static byte[] Serialize<T>(T value, CborSerializerOptions? options = null)
    {
#if NET9_0_OR_GREATER
        Span<byte> scratch = stackalloc byte[256];
        var buffer = new ArrayPoolListWriteBuffer(scratch);
#else
        var buffer = new CompatibleArrayPoolListWriteBuffer();
#endif
        try
        {
            Serialize(ref buffer, value, options);
            return buffer.ToArray();
        }
        finally
        {
            buffer.Dispose();
        }
    }

    /// <summary>Serializes into a caller's writer; the writer is not disposed.</summary>
    public static void Serialize<T>(IBufferWriter<byte> destination, T value, CborSerializerOptions? options = null)
    {
#if NET9_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(destination);
#else
        if (destination is null)
        {
            throw new ArgumentNullException(nameof(destination));
        }
#endif

#if NET9_0_OR_GREATER
        var buffer = new BufferWriterWriteBuffer(destination);
#else
        var buffer = new CompatibleBufferWriterWriteBuffer(destination);
#endif
        try
        {
            Serialize(ref buffer, value, options);
            buffer.Flush();
        }
        finally
        {
            buffer.Dispose();
        }
    }

    /// <summary>Serializes exactly one item into a borrowed Foundation buffer. Does not flush or dispose it.</summary>
    public static void Serialize<TWriteBuffer, T>(ref TWriteBuffer buffer, T value, CborSerializerOptions? options = null)
        where TWriteBuffer : struct, IWriteBuffer
#if NET9_0_OR_GREATER
            , allows ref struct
#endif
    {
        var context = new CborSerializationContext(options ?? CborSerializerOptions.Default, buffer.BytesWritten);
        context.Serialize(ref buffer, value);
    }

    /// <summary>Deserializes a borrowed span without first copying or validating the complete item.</summary>
    public static unsafe T Deserialize<T>(ReadOnlySpan<byte> source, CborSerializerOptions? options = null)
    {
#if NET9_0_OR_GREATER
        var buffer = new ReadOnlySpanReadBuffer(source);
        try
        {
            return Deserialize<ReadOnlySpanReadBuffer, T>(ref buffer, options);
        }
        finally
        {
            buffer.Dispose();
        }
#else
        fixed (byte* pointer = source)
        {
            var buffer = new CompatibleReadOnlySpanReadBuffer(pointer, source.Length);
            try
            {
                return Deserialize<CompatibleReadOnlySpanReadBuffer, T>(ref buffer, options);
            }
            finally
            {
                buffer.Dispose();
            }
        }
#endif
    }

    /// <summary>Deserializes segmented input without flattening it.</summary>
    public static T Deserialize<T>(in ReadOnlySequence<byte> source, CborSerializerOptions? options = null)
    {
#if NET9_0_OR_GREATER
        Span<byte> scratch = stackalloc byte[CborPrimitives.MaxHeaderLength];
        var buffer = new ReadOnlySequenceReadBuffer(in source, scratch);
        try
        {
            return Deserialize<ReadOnlySequenceReadBuffer, T>(ref buffer, options);
        }
#else
        var buffer = new CompatibleReadOnlySequenceReadBuffer(in source);
        try
        {
            return Deserialize<CompatibleReadOnlySequenceReadBuffer, T>(ref buffer, options);
        }
#endif
        finally
        {
            buffer.Dispose();
        }
    }

    /// <summary>Deserializes exactly one item from a bounded, borrowed buffer; does not dispose it.</summary>
    public static T Deserialize<TReadBuffer, T>(ref TReadBuffer buffer, CborSerializerOptions? options = null)
        where TReadBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
            , allows ref struct
#endif
    {
        var context = new CborDeserializationContext(options ?? CborSerializerOptions.Default, buffer.BytesRemaining);
        T value = context.Deserialize<TReadBuffer, T>(ref buffer);
        if (buffer.BytesRemaining != 0)
        {
            throw new InvalidDataException("Trailing bytes follow the CBOR item.");
        }

        return value;
    }
}
