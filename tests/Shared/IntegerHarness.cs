using System.Buffers;
using SerializerFoundation;

namespace Cbor.Testing;

internal static class IntegerHarness
{
    internal static byte[] Encode(ulong value)
    {
#if NET9_0_OR_GREATER
        Span<byte> scratch = stackalloc byte[16];
        var buffer = new ArrayPoolListWriteBuffer(scratch);
#else
        var buffer = new CompatibleArrayPoolListWriteBuffer();
#endif
        try
        {
            buffer.WriteUInt64(value);
            return buffer.ToArray();
        }
        finally
        {
            buffer.Dispose();
        }
    }

    internal static void EncodeTo(IBufferWriter<byte> output, ulong value)
    {
#if NET9_0_OR_GREATER
        var buffer = new BufferWriterWriteBuffer(output);
#else
        var buffer = new CompatibleBufferWriterWriteBuffer(output);
#endif
        try
        {
            buffer.WriteUInt64(value);
            buffer.Flush();
        }
        finally
        {
            buffer.Dispose();
        }
    }

    internal static ulong Decode(byte[] source) => Decode(new ReadOnlySequence<byte>(source));

    internal static ulong Decode(in ReadOnlySequence<byte> source)
    {
#if NET9_0_OR_GREATER
        Span<byte> scratch = stackalloc byte[16];
        var buffer = new ReadOnlySequenceReadBuffer(source, scratch);
#else
        var buffer = new CompatibleReadOnlySequenceReadBuffer(source);
#endif
        try
        {
            ulong value = buffer.ReadUInt64();
            if (buffer.BytesRemaining != 0)
            {
                throw new InvalidDataException("Trailing data after a single CBOR integer.");
            }

            return value;
        }
        finally
        {
            buffer.Dispose();
        }
    }

    internal static ReadOnlySequence<byte> Split(byte[] source, int offset)
    {
        var first = new Segment(source.AsMemory(0, offset));
        var last = first.Append(source.AsMemory(offset));
        return new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);
    }

    internal static ReadOnlySequence<byte> Fragment(byte[] source)
    {
        var first = new Segment(ReadOnlyMemory<byte>.Empty);
        var last = first;
        for (int offset = 0; offset < source.Length; offset++)
        {
            last = last.Append(source.AsMemory(offset, 1));
            last = last.Append(ReadOnlyMemory<byte>.Empty);
        }

        return new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);
    }

    private sealed class Segment : ReadOnlySequenceSegment<byte>
    {
        internal Segment(ReadOnlyMemory<byte> memory)
        {
            Memory = memory;
        }

        internal Segment Append(ReadOnlyMemory<byte> value)
        {
            var next = new Segment(value) { RunningIndex = RunningIndex + Memory.Length };
            Next = next;
            return next;
        }
    }
}
