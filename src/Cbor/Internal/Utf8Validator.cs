using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Buffers.Binary;
#if NET9_0_OR_GREATER
using System.Text;
#endif

namespace Cbor.Internal;

// One state per definite text string. State may cross buffer seams, but never CBOR chunk boundaries.
internal struct Utf8Validator
{
    private const byte AsciiLimit = 0x80;
    private const byte ContinuationMask = 0xc0;
    private const byte ContinuationPrefix = 0x80;
    private const byte ContinuationPayloadMask = 0x3f;
    private const int ContinuationPayloadBits = 6;
    private const byte TwoByteLeadMinimum = 0xc2;
    private const byte TwoByteLeadMaximum = 0xdf;
    private const byte ThreeByteLeadMinimum = 0xe0;
    private const byte ThreeByteLeadMaximum = 0xef;
    private const byte FourByteLeadMinimum = 0xf0;
    private const byte FourByteLeadMaximum = 0xf4;
    private const byte SurrogateLead = 0xed;
    private const byte ThreeByteBoundaryContinuation = 0xa0;
    private const byte FourByteBoundaryContinuation = 0x90;
    private const uint NonAsciiWordMask = 0x80808080;
    private const uint TwoByteContinuationMask = 0x0000c000;
    private const uint TwoByteContinuationPattern = 0x00008000;
    private const uint ThreeByteContinuationMask = 0x00c0c000;
    private const uint ThreeByteContinuationPattern = 0x00808000;
    private const uint FourByteContinuationMask = 0xc0c0c000;
    private const uint FourByteContinuationPattern = 0x80808000;

    private int remaining;
    private uint codePoint;
    private uint minimum;

    internal readonly bool IsComplete => remaining == 0;

    internal bool Append(ReadOnlySpan<byte> bytes)
    {
        int offset = 0;
        while (remaining != 0 && offset < bytes.Length)
        {
            if (!AppendScalar(bytes.Slice(offset++, 1)))
            {
                return false;
            }
        }

        bytes = bytes.Slice(offset);
#if NET9_0_OR_GREATER
        if (remaining == 0 && Ascii.IsValid(bytes))
        {
            return true;
        }
#endif

        offset = 0;
        while (bytes.Length - offset >= sizeof(uint))
        {
            uint word = Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref MemoryMarshal.GetReference(bytes), offset));
            if (!BitConverter.IsLittleEndian)
            {
                word = BinaryPrimitives.ReverseEndianness(word);
            }

            byte lead = (byte)word;
            if (lead < AsciiLimit)
            {
                offset += (word & NonAsciiWordMask) == 0 ? sizeof(uint) : 1;
            }
            else if (lead is >= TwoByteLeadMinimum and <= TwoByteLeadMaximum)
            {
                if ((word & TwoByteContinuationMask) != TwoByteContinuationPattern)
                {
                    return false;
                }

                offset += 2;
            }
            else if (lead is >= ThreeByteLeadMinimum and <= ThreeByteLeadMaximum)
            {
                byte second = (byte)(word >> CborEncoding.BitsPerByte);
                if ((word & ThreeByteContinuationMask) != ThreeByteContinuationPattern ||
                    (lead == ThreeByteLeadMinimum && second < ThreeByteBoundaryContinuation) ||
                    (lead == SurrogateLead && second >= ThreeByteBoundaryContinuation))
                {
                    return false;
                }

                offset += 3;
            }
            else if (lead is >= FourByteLeadMinimum and <= FourByteLeadMaximum)
            {
                byte second = (byte)(word >> CborEncoding.BitsPerByte);
                if ((word & FourByteContinuationMask) != FourByteContinuationPattern ||
                    (lead == FourByteLeadMinimum && second < FourByteBoundaryContinuation) ||
                    (lead == FourByteLeadMaximum && second >= FourByteBoundaryContinuation))
                {
                    return false;
                }

                offset += 4;
            }
            else
            {
                return false;
            }
        }

        // The remaining zero to three bytes may be a partial code point. Only this
        // tail and pending continuations at the next seam need byte-by-byte state.
        return AppendScalar(bytes.Slice(offset));
    }

    private bool AppendScalar(ReadOnlySpan<byte> bytes)
    {
        int offset = 0;
        while (offset < bytes.Length)
        {
            byte value = bytes[offset++];
            if (remaining == 0)
            {
                if (value < AsciiLimit)
                {
                    continue;
                }

                if (value is >= TwoByteLeadMinimum and <= TwoByteLeadMaximum)
                {
                    remaining = 1;
                    codePoint = (uint)(value & 0x1f);
                    minimum = 0x80;
                }
                else if (value is >= ThreeByteLeadMinimum and <= ThreeByteLeadMaximum)
                {
                    remaining = 2;
                    codePoint = (uint)(value & 0x0f);
                    minimum = 0x800;
                }
                else if (value is >= FourByteLeadMinimum and <= FourByteLeadMaximum)
                {
                    remaining = 3;
                    codePoint = (uint)(value & 7);
                    minimum = 0x10000;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                if ((value & ContinuationMask) != ContinuationPrefix)
                {
                    return false;
                }

                codePoint = (codePoint << ContinuationPayloadBits) | (uint)(value & ContinuationPayloadMask);
                remaining--;
                if (remaining == 0 && (codePoint < minimum || codePoint > 0x10ffff || codePoint is >= 0xd800 and <= 0xdfff))
                {
                    return false;
                }
            }
        }

        return true;
    }

    internal static bool IsValid(ReadOnlySpan<byte> bytes)
    {
        var state = new Utf8Validator();
        return state.Append(bytes) && state.IsComplete;
    }
}
