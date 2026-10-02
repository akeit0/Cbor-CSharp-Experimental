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
        if (options.UseCompatibleBuffers || !options.Resolver.TryGetFormatter<ArrayPoolListWriteBuffer, ReadOnlySpanReadBuffer, T>(out var formatter)) { return SerializeArrayCompatible(value, options); }
        Span<byte> scratch = stackalloc byte[SerializeScratchSize];
        var buffer = new ArrayPoolListWriteBuffer(scratch);
#else
        var buffer = new CompatibleArrayPoolListWriteBuffer();
        var formatter = options.Resolver.GetFormatter<CompatibleArrayPoolListWriteBuffer, CompatibleReadOnlySpanReadBuffer, T>();
#endif
        var context = new CborSerializationContext(options ?? CborSerializerOptions.Default);
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
        if (options.UseCompatibleBuffers || !options.Resolver.TryGetFormatter<BufferWriterWriteBuffer, ReadOnlySpanReadBuffer, T>(out var formatter)) { SerializeWriterCompatible(destination, value, options); return; }
        var buffer = new BufferWriterWriteBuffer(destination);
#else
        var buffer = new CompatibleBufferWriterWriteBuffer(destination);
        var formatter = options.Resolver.GetFormatter<CompatibleBufferWriterWriteBuffer, CompatibleReadOnlySpanReadBuffer, T>();
#endif
        var context = new CborSerializationContext(options ?? CborSerializerOptions.Default);
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
            context.Serialize(ref buffer, value);
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
        if (options.UseCompatibleBuffers || !options.Resolver.TryGetFormatter<ArrayPoolListWriteBuffer, ReadOnlySpanReadBuffer, T>(out var formatter)) { return DeserializeSpanCompatible<T>(source, options); }
        var buffer = new ReadOnlySpanReadBuffer(source);
        var context = new CborDeserializationContext(options ?? CborSerializerOptions.Default, source.Length);
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
            var context = new CborDeserializationContext(options ?? CborSerializerOptions.Default, source.Length);
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
        if (options.UseCompatibleBuffers || !options.Resolver.TryGetFormatter<ArrayPoolListWriteBuffer, ReadOnlySequenceReadBuffer, T>(out var formatter)) { return DeserializeSequenceCompatible<T>(in source, options); }
#endif
        var context = new CborDeserializationContext(options ?? CborSerializerOptions.Default, source.Length);
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
            return DeserializeCore<CompatibleArrayPoolListWriteBuffer, TReadBuffer, T>(ref buffer, ref context, context.Options.Resolver.GetFormatter<CompatibleArrayPoolListWriteBuffer, TReadBuffer, T>());
        }
        finally
        {
            context.Dispose();
        }
    }

    /// <summary>Serializes through a borrowed ordinary struct buffer, including older-target formatters. Does not flush or dispose it.</summary>
    public static void SerializeCompatible<TWriteBuffer, T>(ref TWriteBuffer buffer, T value, CborSerializerOptions? options = null)
        where TWriteBuffer : struct, IWriteBuffer
    {
        var context = new CborSerializationContext(options ?? CborSerializerOptions.Default, buffer.BytesWritten);
        try
        {
            context.Serialize(ref buffer, value);
        }
        finally { context.Dispose(); }
    }

    /// <summary>Deserializes exactly one item through a borrowed ordinary struct buffer, including older-target formatters.</summary>
    public static T DeserializeCompatible<TReadBuffer, T>(ref TReadBuffer buffer, CborSerializerOptions? options = null)
        where TReadBuffer : struct, IReadBuffer
    {
        var context = new CborDeserializationContext(options ?? CborSerializerOptions.Default, buffer.BytesRemaining);
        try
        {
            T value = context.Deserialize(ref buffer, context.Options.Resolver.GetFormatter<CompatibleArrayPoolListWriteBuffer, TReadBuffer, T>());
            if (buffer.BytesRemaining != 0) { throw new InvalidDataException("Trailing bytes follow the CBOR item."); }
            return value;
        }
        finally { context.Dispose(); }
    }

#if NET9_0_OR_GREATER
    private static byte[] SerializeArrayCompatible<T>(T value, CborSerializerOptions options)
    {
        var buffer = new CompatibleArrayPoolListWriteBuffer();
        var context = new CborSerializationContext(options);

        try
        {
            context.Serialize(ref buffer, value, options.Resolver.GetFormatter<CompatibleArrayPoolListWriteBuffer, CompatibleReadOnlySpanReadBuffer, T>());
            return buffer.ToArray();
        }
        finally { context.Dispose(); buffer.Dispose(); }
    }

    private static void SerializeWriterCompatible<T>(IBufferWriter<byte> destination, T value, CborSerializerOptions options)
    {
        var buffer = new CompatibleBufferWriterWriteBuffer(destination);
        var context = new CborSerializationContext(options);

        try
        {
            context.Serialize(ref buffer, value, options.Resolver.GetFormatter<CompatibleBufferWriterWriteBuffer, CompatibleReadOnlySpanReadBuffer, T>());
            buffer.Flush();
        }
        finally { context.Dispose(); buffer.Dispose(); }
    }

    private static unsafe T DeserializeSpanCompatible<T>(ReadOnlySpan<byte> source, CborSerializerOptions options)
    {
        fixed (byte* pointer = source)
        {
            var buffer = new CompatibleReadOnlySpanReadBuffer(pointer, source.Length);
            var context = new CborDeserializationContext(options, source.Length);

            try { return DeserializeCore<CompatibleArrayPoolListWriteBuffer, CompatibleReadOnlySpanReadBuffer, T>(ref buffer, ref context, context.Options.Resolver.GetFormatter<CompatibleArrayPoolListWriteBuffer, CompatibleReadOnlySpanReadBuffer, T>()); }
            finally { context.Dispose(); buffer.Dispose(); }
        }
    }

    private static T DeserializeSequenceCompatible<T>(in ReadOnlySequence<byte> source, CborSerializerOptions options)
    {
        var buffer = new CompatibleReadOnlySequenceReadBuffer(in source);
        var context = new CborDeserializationContext(options, source.Length);

        try { return DeserializeCore<CompatibleArrayPoolListWriteBuffer, CompatibleReadOnlySequenceReadBuffer, T>(ref buffer, ref context, context.Options.Resolver.GetFormatter<CompatibleArrayPoolListWriteBuffer, CompatibleReadOnlySequenceReadBuffer, T>()); }
        finally { context.Dispose(); buffer.Dispose(); }
    }
#endif

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
