using System.Buffers;
using SerializerFoundation;

namespace Cbor.Internal;

internal sealed class StringFormatter<TWriteBuffer, TReadBuffer> :
    ICborFormatter<TWriteBuffer, TReadBuffer, string?>
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
    public void Serialize(ref TWriteBuffer buffer, ref CborSerializationContext context, string? value)
    {
        if (value is null)
        {
            buffer.WriteNull();
            return;
        }

        int byteCount = CborTextEncoding.StrictUtf8.GetByteCount(value);
        context.CheckStringLength(byteCount);
        context.CheckEncodedLength(buffer.BytesWritten, (long)byteCount + CborPrimitives.GetHeaderLength((ulong)byteCount));
        CborWriteBufferExtensions.WriteTextStringKnownLength(ref buffer, value, byteCount);
    }
    public string? Deserialize(ref TReadBuffer buffer, ref CborDeserializationContext context)
    {
        if (CborDeserializationContext.TryReadNull(ref buffer))
        {
            return null;
        }

        var span = buffer.GetUnreadSpan();
        if (span.IsEmpty || span[0] != CborEncoding.IndefiniteTextString)
        {
            return buffer.ReadTextString(context.Options.MaxStringLength);
        }

        return ChunkedStringReader.Read<TReadBuffer, string, Utf8StringMaterializer>(ref buffer, ref context);
    }
}

internal sealed class ByteStringFormatter<TWriteBuffer, TReadBuffer> :
    ICborFormatter<TWriteBuffer, TReadBuffer, byte[]?>
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
    public void Serialize(ref TWriteBuffer buffer, ref CborSerializationContext context, byte[]? value)
    {
        if (value is null)
        {
            buffer.WriteNull();
            return;
        }

        context.CheckStringLength(value.Length);
        context.CheckEncodedLength(buffer.BytesWritten, (long)value.Length + CborPrimitives.GetHeaderLength((ulong)value.Length));
        buffer.WriteByteString(value);
    }
    public byte[]? Deserialize(ref TReadBuffer buffer, ref CborDeserializationContext context)
    {
        if (CborDeserializationContext.TryReadNull(ref buffer))
        {
            return null;
        }

        var span = buffer.GetUnreadSpan();
        if (span.IsEmpty || span[0] != CborEncoding.IndefiniteByteString)
        {
            return buffer.ReadByteString(context.Options.MaxStringLength);
        }

        return ChunkedStringReader.Read<TReadBuffer, byte[], ByteStringMaterializer>(ref buffer, ref context);
    }
}

internal static class ChunkedStringReader
{
    internal static TResult Read<TReadBuffer, TResult, TMaterializer>(ref TReadBuffer buffer, ref CborDeserializationContext context)
        where TReadBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
            , allows ref struct
#endif
        where TMaterializer : struct, IStringMaterializer<TResult>
    {
        var materializer = default(TMaterializer);
        CborMajorType major = materializer.MajorType;
        var header = CborReadBufferExtensions.PeekLengthHeader(ref buffer, major);

        context.EnterContainer();
        byte[]? rented = null;
        try
        {
            buffer.Advance(header.EncodedLength);
            int length = 0;
            int limit = context.Options.MaxStringLength;
            rented = ArrayPool<byte>.Shared.Rent(Math.Max(1, Math.Min(256, limit)));
            while (!buffer.TryReadBreak())
            {
                header = CborReadBufferExtensions.PeekLengthHeader(ref buffer, major);
                context.ChargeItem();
                context.CheckHeader(header);
                if (header.MajorType != major || header.IsIndefiniteLength)
                {
                    CborError.Throw(CborDecodeResult.InvalidData);
                }

                if (header.Argument > (ulong)(limit - length))
                {
                    CborError.Throw(CborDecodeResult.LimitExceeded);
                }

                if (header.Argument > (ulong)(buffer.BytesRemaining - header.EncodedLength))
                {
                    CborError.Throw(CborDecodeResult.NeedMoreData);
                }

                int chunkLength = (int)header.Argument;
                int required = length + chunkLength;
                if (required > rented.Length)
                {
                    int capacity = (int)Math.Min(limit, Math.Max((long)required, (long)rented.Length * 2));
                    byte[] larger = ArrayPool<byte>.Shared.Rent(capacity);
                    rented.AsSpan(0, length).CopyTo(larger);
                    ArrayPool<byte>.Shared.Return(rented, clearArray: true);
                    rented = larger;
                }

                buffer.Advance(header.EncodedLength);
                buffer.CopyTo(rented.AsSpan(length, chunkLength));
                buffer.Advance(chunkLength);
                // UTF-8 is valid per chunk, rather than merely across the concatenation.
                if (major == CborMajorType.TextString && !Utf8Validator.IsValid(rented.AsSpan(length, chunkLength)))
                {
                    CborError.Throw(CborDecodeResult.InvalidData);
                }

                length = required;
            }

            return materializer.Materialize(rented, length);
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<byte>.Shared.Return(rented, clearArray: true);
            }

            context.ExitContainer();
        }
    }
}

// Constrained struct dispatch specializes the shared traversal without type-erasing its result.
// Instance members keep this strategy usable on the .NET Standard compatibility tier as well.
internal interface IStringMaterializer<TResult>
{
    CborMajorType MajorType { get; }

    TResult Materialize(byte[] source, int length);
}

internal readonly struct Utf8StringMaterializer : IStringMaterializer<string>
{
    public CborMajorType MajorType => CborMajorType.TextString;

    public string Materialize(byte[] source, int length) => CborTextEncoding.StrictUtf8.GetString(source, 0, length);
}

internal readonly struct ByteStringMaterializer : IStringMaterializer<byte[]>
{
    public CborMajorType MajorType => CborMajorType.ByteString;

    public byte[] Materialize(byte[] source, int length)
    {
        var result = new byte[length];
        source.AsSpan(0, length).CopyTo(result);
        return result;
    }
}
