using System.Buffers;
using SerializerFoundation;

namespace Cbor;

/// <summary>Typed CBOR serialization over spans, sequences, and caller-owned buffer writers.</summary>
/// <remarks>Deserialization consumes exactly one item and rejects trailing bytes. Formatter exceptions
/// propagate unchanged. Streaming writes may commit a prefix before an error; callers own rollback.</remarks>
public static class CborSerializer
{
    private const int SerializeScratchSize = 256;
    /// <summary>Serializes a value to an independently owned byte array.</summary>
    public static byte[] Serialize<T>(T value, CborSerializerOptions? options = null)
    {
        options ??= CborSerializerOptions.Default;
#if NET9_0_OR_GREATER
        var formatter = options.Resolver.GetFormatter<ArrayPoolListWriteBuffer, ReadOnlySpanReadBuffer, T>();
        Span<byte> scratch = stackalloc byte[SerializeScratchSize];
        var buffer = new ArrayPoolListWriteBuffer(scratch);
#else
        var formatter = options.Resolver.GetFormatter<CompatibleArrayPoolListWriteBuffer, CompatibleReadOnlySpanReadBuffer, T>();
        var buffer = new CompatibleArrayPoolListWriteBuffer();
#endif
        var context = new CborSerializationContext(options);
        try
        {
            context.Serialize(ref buffer, value, formatter);
            return buffer.ToArray();
        }
        finally
        {
            context.Dispose();
            buffer.Dispose();
        }
    }

    /// <summary>Serializes into a caller's writer; the writer is not disposed.</summary>
    public static void Serialize<T>(IBufferWriter<byte> destination, T value, CborSerializerOptions? options = null)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(destination);
#else
        if (destination is null)
        {
            throw new ArgumentNullException(nameof(destination));
        }
#endif

        options ??= CborSerializerOptions.Default;
#if NET9_0_OR_GREATER
        var formatter = options.Resolver.GetFormatter<BufferWriterWriteBuffer, ReadOnlySpanReadBuffer, T>();
        var buffer = new BufferWriterWriteBuffer(destination);
#else
        var formatter = options.Resolver.GetFormatter<CompatibleBufferWriterWriteBuffer, CompatibleReadOnlySpanReadBuffer, T>();
        var buffer = new CompatibleBufferWriterWriteBuffer(destination);
#endif
        var context = new CborSerializationContext(options);
        try
        {
            context.Serialize(ref buffer, value, formatter);
            buffer.Flush();
        }
        finally
        {
            context.Dispose();
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
        try
        {
#if NET9_0_OR_GREATER
            context.Serialize(ref buffer, value, context.Options.Resolver.GetFormatter<TWriteBuffer, ReadOnlySpanReadBuffer, T>());
#else
            context.Serialize(ref buffer, value, context.Options.Resolver.GetFormatter<TWriteBuffer, CompatibleReadOnlySpanReadBuffer, T>());
#endif
        }
        finally
        {
            context.Dispose();
        }
    }

    /// <summary>Deserializes a borrowed span without first copying or validating the complete item.</summary>
    public static unsafe T Deserialize<T>(ReadOnlySpan<byte> source, CborSerializerOptions? options = null)
    {
        options ??= CborSerializerOptions.Default;
#if NET9_0_OR_GREATER
        var formatter = options.Resolver.GetFormatter<ArrayPoolListWriteBuffer, ReadOnlySpanReadBuffer, T>();
        var buffer = new ReadOnlySpanReadBuffer(source);
        var context = new CborDeserializationContext(options, source.Length);
        try
        {
            return DeserializeCore<ArrayPoolListWriteBuffer, ReadOnlySpanReadBuffer, T>(ref buffer, ref context, formatter);
        }
        finally
        {
            context.Dispose();
            buffer.Dispose();
        }
#else
        fixed (byte* pointer = source)
        {
            var buffer = new CompatibleReadOnlySpanReadBuffer(pointer, source.Length);
            var context = new CborDeserializationContext(options, source.Length);
            try
            {
                return DeserializeCore<CompatibleArrayPoolListWriteBuffer, CompatibleReadOnlySpanReadBuffer, T>(ref buffer, ref context, context.Options.Resolver.GetFormatter<CompatibleArrayPoolListWriteBuffer, CompatibleReadOnlySpanReadBuffer, T>());
            }
            finally
            {
                context.Dispose();
                buffer.Dispose();
            }
        }
#endif
    }

    /// <summary>Deserializes segmented input without flattening it.</summary>
    public static T Deserialize<T>(in ReadOnlySequence<byte> source, CborSerializerOptions? options = null)
    {
        options ??= CborSerializerOptions.Default;
#if NET9_0_OR_GREATER
        var formatter = options.Resolver.GetFormatter<ArrayPoolListWriteBuffer, ReadOnlySequenceReadBuffer, T>();
#endif
        var context = new CborDeserializationContext(options, source.Length);
#if NET9_0_OR_GREATER
        Span<byte> scratch = stackalloc byte[CborPrimitives.MaxHeaderLength];
        var buffer = new ReadOnlySequenceReadBuffer(in source, scratch);
        try
        {
            return DeserializeCore<ArrayPoolListWriteBuffer, ReadOnlySequenceReadBuffer, T>(ref buffer, ref context, formatter);
        }
#else
        var buffer = new CompatibleReadOnlySequenceReadBuffer(in source);
        try
        {
            return DeserializeCore<CompatibleArrayPoolListWriteBuffer, CompatibleReadOnlySequenceReadBuffer, T>(ref buffer, ref context, context.Options.Resolver.GetFormatter<CompatibleArrayPoolListWriteBuffer, CompatibleReadOnlySequenceReadBuffer, T>());
        }
#endif
        finally
        {
            context.Dispose();
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
        try
        {
#if NET9_0_OR_GREATER
            return DeserializeCore<ArrayPoolListWriteBuffer, TReadBuffer, T>(ref buffer, ref context, context.Options.Resolver.GetFormatter<ArrayPoolListWriteBuffer, TReadBuffer, T>());
#else
            return DeserializeCore<CompatibleArrayPoolListWriteBuffer, TReadBuffer, T>(ref buffer, ref context, context.Options.Resolver.GetFormatter<CompatibleArrayPoolListWriteBuffer, TReadBuffer, T>());
#endif
        }
        finally
        {
            context.Dispose();
        }
    }

    private static T DeserializeCore<TWriteBuffer, TReadBuffer, T>(ref TReadBuffer buffer, ref CborDeserializationContext context, ICborFormatter<TWriteBuffer, TReadBuffer, T> formatter)
        where TWriteBuffer : struct, IWriteBuffer
#if NET9_0_OR_GREATER
            , allows ref struct
#endif
        where TReadBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
            , allows ref struct
#endif
    {
        T value = context.Deserialize(ref buffer, formatter);
        if (buffer.BytesRemaining != 0)
        {
            throw new InvalidDataException("Trailing bytes follow the CBOR item.");
        }

        return value;
    }
}
