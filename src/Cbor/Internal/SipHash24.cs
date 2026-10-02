using System.Buffers.Binary;

namespace Cbor.Internal;

// SipHash-2-4 with a 128-bit key. Algorithm and known-answer vectors:
// https://github.com/veorq/SipHash (CC0 reference by Aumasson and Bernstein).
internal static class SipHash24
{
    internal static ulong Hash(ReadOnlySpan<byte> source, ulong key0, ulong key1)
    {
        unchecked
        {
            ulong a = 0x736f6d6570736575UL ^ key0;
            ulong b = 0x646f72616e646f6dUL ^ key1;
            ulong c = 0x6c7967656e657261UL ^ key0;
            ulong d = 0x7465646279746573UL ^ key1;
            int offset = 0;
            while (source.Length - offset >= 8)
            {
                ulong block = BinaryPrimitives.ReadUInt64LittleEndian(source.Slice(offset, 8));
                Compress(ref a, ref b, ref c, ref d, block);
                offset += 8;
            }

            ulong tail = (ulong)source.Length << 56;
            for (int i = 0; i < source.Length - offset; i++)
            {
                tail |= (ulong)source[offset + i] << (i * 8);
            }

            Compress(ref a, ref b, ref c, ref d, tail);
            c ^= 255;
            for (int i = 0; i < 4; i++)
            {
                Round(ref a, ref b, ref c, ref d);
            }

            return a ^ b ^ c ^ d;
        }
    }

    private static void Compress(ref ulong a, ref ulong b, ref ulong c, ref ulong d, ulong block)
    {
        d ^= block;
        Round(ref a, ref b, ref c, ref d);
        Round(ref a, ref b, ref c, ref d);
        a ^= block;
    }

    private static void Round(ref ulong a, ref ulong b, ref ulong c, ref ulong d)
    {
        unchecked
        {
            a += b;
            b = Rotate(b, 13) ^ a;
            a = Rotate(a, 32);
            c += d;
            d = Rotate(d, 16) ^ c;
            a += d;
            d = Rotate(d, 21) ^ a;
            c += b;
            b = Rotate(b, 17) ^ c;
            c = Rotate(c, 32);
        }
    }

    private static ulong Rotate(ulong bits, int shift) => (bits << shift) | (bits >> (64 - shift));
}
