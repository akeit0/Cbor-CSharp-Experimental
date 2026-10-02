using System.Buffers;
using System.Numerics;
using SerializerFoundation;

namespace Cbor.Internal;

internal sealed class IntegerFormatter<TWriteBuffer, TReadBuffer> :
    ICborFormatter<TWriteBuffer, TReadBuffer, CborInteger>
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
    public void Serialize(ref TWriteBuffer buffer, ref CborSerializationContext context, CborInteger value)
    {
        if (value.IsNegative)
        {
            buffer.WriteNegativeIntegerArgument(value.Argument);
        }
        else
        {
            buffer.WriteUInt64(value.Argument);
        }
    }
    public CborInteger Deserialize(ref TReadBuffer buffer, ref CborDeserializationContext context)
    {
        var span = buffer.GetUnreadSpan();
        if (!span.IsEmpty && span[0] < CborEncoding.NegativeIntegerPrefix)
        {
            return new(buffer.ReadUInt64());
        }
        return CborInteger.FromNegativeArgument(buffer.ReadNegativeIntegerArgument());
    }
}

internal sealed class SimpleValueFormatter<TWriteBuffer, TReadBuffer> :
    ICborFormatter<TWriteBuffer, TReadBuffer, CborSimpleValue>
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
    public void Serialize(ref TWriteBuffer buffer, ref CborSerializationContext context, CborSimpleValue value)
        => buffer.WriteSimpleValue(value.Value);
    public CborSimpleValue Deserialize(ref TReadBuffer buffer, ref CborDeserializationContext context)
        => new(buffer.ReadSimpleValue());
}

internal sealed class BigIntegerFormatter<TWriteBuffer, TReadBuffer> :
    ICborFormatter<TWriteBuffer, TReadBuffer, BigInteger>
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
    private const ulong PositiveBignumTag = 2;
    private const ulong NegativeBignumTag = 3;
    private const int StackMagnitudeLength = 256;
    private const byte SignedByteMask = 0x80;
    private static readonly ByteStringFormatter<TWriteBuffer, TReadBuffer> MagnitudeFormatter = new();
    private static readonly BigInteger MaximumIntegerArgument = new(ulong.MaxValue);
    private static readonly BigInteger MinimumSignedInteger = new(long.MinValue);
    public void Serialize(ref TWriteBuffer buffer, ref CborSerializationContext context, BigInteger value)
    {
        bool negative = value.Sign < 0;
        if (negative && value >= MinimumSignedInteger)
        {
            buffer.WriteInt64((long)value);
            return;
        }
        BigInteger magnitude = negative ? ~value : value;
        if (magnitude <= MaximumIntegerArgument)
        {
            if (negative)
            {
                buffer.WriteNegativeIntegerArgument((ulong)magnitude);
            }
            else
            {
                buffer.WriteUInt64((ulong)magnitude);
            }
            return;
        }

        context.EnterContainer();
        byte[]? rented = null;
        try
        {
#if NETSTANDARD2_0
            // This reference assembly has only the signed little-endian byte-array API.
            byte[] bytes = magnitude.ToByteArray();
            int length = bytes.Length - (bytes[bytes.Length - 1] == 0 ? 1 : 0);
            context.CheckStringLength(length);
            context.CheckEncodedLength(buffer.BytesWritten,
                CborEncoding.ImmediateHeaderLength + CborPrimitives.GetHeaderLength((ulong)length) + (long)length);
            Array.Reverse(bytes, 0, length);
            ReadOnlySpan<byte> payload = bytes.AsSpan(0, length);
#else
            int length = magnitude.GetByteCount(isUnsigned: true);
            context.CheckStringLength(length);
            context.CheckEncodedLength(buffer.BytesWritten,
                CborEncoding.ImmediateHeaderLength + CborPrimitives.GetHeaderLength((ulong)length) + (long)length);
            Span<byte> payload = length <= StackMagnitudeLength ? stackalloc byte[StackMagnitudeLength] :
                (rented = ArrayPool<byte>.Shared.Rent(length));
            payload = payload.Slice(0, length);
            if (!magnitude.TryWriteBytes(payload, out _, isUnsigned: true, isBigEndian: true))
            {
                throw new InvalidOperationException("The bignum magnitude did not fit its computed length.");
            }
#endif
            // The tag's byte-string content is structural; CLR byte[] overrides do not change this contract.
            context.ChargeItem();
            buffer.WriteTag(negative ? NegativeBignumTag : PositiveBignumTag);
            buffer.WriteByteString(payload);
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
    public BigInteger Deserialize(ref TReadBuffer buffer, ref CborDeserializationContext context)
    {
        var span = buffer.GetUnreadSpan();
        if (!span.IsEmpty && span[0] < CborEncoding.ByteStringPrefix)
        {
            return span[0] < CborEncoding.NegativeIntegerPrefix ?
                new BigInteger(buffer.ReadUInt64()) : ~new BigInteger(buffer.ReadNegativeIntegerArgument());
        }

        context.EnterContainer();
        try
        {
            ulong tag = buffer.ReadTag();
            if (tag is not (PositiveBignumTag or NegativeBignumTag))
            {
                throw new InvalidDataException("BigInteger requires an integer or CBOR bignum tag 2 or 3.");
            }
            BigInteger magnitude = ReadMagnitude(ref buffer, ref context);
            return tag == NegativeBignumTag ? ~magnitude : magnitude;
        }
        finally
        {
            context.ExitContainer();
        }
    }

    private static BigInteger ReadMagnitude(ref TReadBuffer buffer, ref CborDeserializationContext context)
    {
        var span = buffer.GetUnreadSpan();
        if (!span.IsEmpty && span[0] == CborEncoding.IndefiniteByteString)
        {
            byte[] bytes = context.Deserialize(ref buffer, MagnitudeFormatter)!;
            return DecodeMagnitude(bytes, context.Options.ReaderOptions.RequirePreferredEncoding);
        }

        context.ChargeItem();
        var header = CborReadBufferExtensions.ValidateStringHeader(ref buffer, CborMajorType.ByteString, context.Options.MaxStringLength);
        context.CheckHeader(header);
        int length = (int)header.Argument;
        buffer.Advance(header.EncodedLength);
        if (!buffer.TryGetSpan(length, out span))
        {
            CborError.Throw(CborDecodeResult.NeedMoreData);
        }
        BigInteger result = DecodeMagnitude(span.Slice(0, length), context.Options.ReaderOptions.RequirePreferredEncoding);
        buffer.Advance(length);
        return result;
    }

    private static BigInteger DecodeMagnitude(ReadOnlySpan<byte> bytes, bool preferred)
    {
        if (preferred && (bytes.Length <= sizeof(ulong) || bytes[0] == 0))
        {
            throw new InvalidDataException("The bignum is not in preferred encoding.");
        }
#if NETSTANDARD2_0
        if (bytes.IsEmpty)
        {
            return BigInteger.Zero;
        }
        // One bounded allocation provides the legacy constructor's signed little-endian input.
        var littleEndian = new byte[checked(bytes.Length + ((bytes[0] & SignedByteMask) != 0 ? 1 : 0))];
        for (int i = 0; i < bytes.Length; i++)
        {
            littleEndian[bytes.Length - i - 1] = bytes[i];
        }
        return new BigInteger(littleEndian);
#else
        return new BigInteger(bytes, isUnsigned: true, isBigEndian: true);
#endif
    }
}
