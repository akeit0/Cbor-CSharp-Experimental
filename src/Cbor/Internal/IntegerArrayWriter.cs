using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SerializerFoundation;

namespace Cbor.Internal;

// Only built-in scalar formatters opt into batching. User overrides retain their per-item calls.
internal interface IIntegerArrayWriter<TWriteBuffer, T>
    where TWriteBuffer : struct, IWriteBuffer
#if NET9_0_OR_GREATER
    , allows ref struct
#endif
{
    void WriteElements(ref TWriteBuffer buffer, ref CborSerializationContext context, ReadOnlySpan<T> source);
}

internal interface IIntegerElementWriter<T>
{
    // The caller supplies the nine-byte scratch window required by UnsafeWriteHeader.
    int Write(ref byte destination, T value);
}

internal static class IntegerArrayWriter
{
    internal const int MinimumBatchElements = 2;
    private const int MaximumBatchElements = 1024;

    internal static void WriteElements<TWriteBuffer, TReadBuffer, T, TCodec>(
        ref TWriteBuffer buffer, ref CborSerializationContext context, ReadOnlySpan<T> source,
        ICborFormatter<TWriteBuffer, TReadBuffer, T> formatter)
        where TWriteBuffer : struct, IWriteBuffer
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
        where TReadBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
        where TCodec : struct, IIntegerElementWriter<T>
    {
        int index = 0;
        while (index < source.Length)
        {
            // Charge before any writer call, including one that may acquire storage.
            context.ChargeItem();
            // Reuse the buffer's actual window instead of reserving worst-case storage
            // for the full array. Exact/tight writers retain the scalar allocation path.
            var window = buffer.GetSpan();
            if (window.Length < CborPrimitives.MaxHeaderLength)
            {
                context.SerializeSameItem(ref buffer, source[index++], formatter);
                continue;
            }

            long start = buffer.BytesWritten;
            int written = 0;
            int end = index + Math.Min(source.Length - index, MaximumBatchElements);
            try
            {
                while (true)
                {
                    written += default(TCodec).Write(ref Unsafe.Add(ref MemoryMarshal.GetReference(window), written), source[index++]);
                    context.CheckEncodedLength(start + written);
                    if (index == end || window.Length - written < CborPrimitives.MaxHeaderLength)
                    {
                        break;
                    }
                    context.ChargeItem();
                }
            }
            finally
            {
                // Publish the same completed/offending scalar prefix when a budget fails.
                buffer.Advance(written);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int WriteSigned(ref byte destination, long value)
        => CborTokenEncoder.UnsafeWriteHeader(ref destination,
            value >= 0 ? CborMajorType.UnsignedInteger : CborMajorType.NegativeInteger,
            value >= 0 ? (ulong)value : (ulong)~value);
}

internal readonly struct ByteElementWriter : IIntegerElementWriter<byte>
{
    public int Write(ref byte destination, byte value) => CborTokenEncoder.UnsafeWriteHeader(ref destination, CborMajorType.UnsignedInteger, value);
}

internal readonly struct SByteElementWriter : IIntegerElementWriter<sbyte>
{
    public int Write(ref byte destination, sbyte value) => IntegerArrayWriter.WriteSigned(ref destination, value);
}

internal readonly struct Int16ElementWriter : IIntegerElementWriter<short>
{
    public int Write(ref byte destination, short value) => IntegerArrayWriter.WriteSigned(ref destination, value);
}

internal readonly struct UInt16ElementWriter : IIntegerElementWriter<ushort>
{
    public int Write(ref byte destination, ushort value) => CborTokenEncoder.UnsafeWriteHeader(ref destination, CborMajorType.UnsignedInteger, value);
}

internal readonly struct Int32ElementWriter : IIntegerElementWriter<int>
{
    public int Write(ref byte destination, int value) => IntegerArrayWriter.WriteSigned(ref destination, value);
}

internal readonly struct UInt32ElementWriter : IIntegerElementWriter<uint>
{
    public int Write(ref byte destination, uint value) => CborTokenEncoder.UnsafeWriteHeader(ref destination, CborMajorType.UnsignedInteger, value);
}

internal readonly struct Int64ElementWriter : IIntegerElementWriter<long>
{
    public int Write(ref byte destination, long value) => IntegerArrayWriter.WriteSigned(ref destination, value);
}

internal readonly struct UInt64ElementWriter : IIntegerElementWriter<ulong>
{
    public int Write(ref byte destination, ulong value) => CborTokenEncoder.UnsafeWriteHeader(ref destination, CborMajorType.UnsignedInteger, value);
}
