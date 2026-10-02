using SerializerFoundation;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using Cbor.Internal;

namespace Cbor;

#if NET8_0_OR_GREATER
/// <summary>Formats Half using preferred floating-point encoding, with checked finite-range narrowing on read.</summary>
public sealed class CborHalfFormatter<TWriteBuffer, TReadBuffer> :
    ICborFormatter<TWriteBuffer, TReadBuffer, Half>
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
    /// <inheritdoc />
    public void Serialize(ref TWriteBuffer buffer, ref CborSerializationContext context, Half value)
        => buffer.WriteHalfBits(Half.IsNaN(value) ? CborEncoding.CanonicalHalfNaN : BitConverter.HalfToUInt16Bits(value));

    /// <inheritdoc />
    public Half Deserialize(ref TReadBuffer buffer, ref CborDeserializationContext context)
    {
        var span = buffer.GetUnreadSpan();
        if (span.Length >= CborEncoding.Float16Length && span[0] == CborEncoding.Float16)
        {
            ushort bits = BinaryPrimitives.ReadUInt16BigEndian(span.Slice(CborEncoding.ArgumentOffset));
            buffer.Advance(CborEncoding.Float16Length);
            return BitConverter.UInt16BitsToHalf(bits);
        }
        return ReadWiderOrFragmented(ref buffer);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Half ReadWiderOrFragmented<TBuffer>(ref TBuffer buffer)
        where TBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
            , allows ref struct
#endif
    {
        double value = buffer.ReadDouble();
        if (!double.IsInfinity(value) && (value > (double)Half.MaxValue || value < (double)Half.MinValue))
        {
            throw new OverflowException("The CBOR floating-point value exceeds Half's finite range.");
        }
        return (Half)value;
    }
}
#endif
