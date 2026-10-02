using System.Buffers;
using SerializerFoundation;

namespace Cbor.Internal;

// Iterative grammar traversal. No recursion and no allocations proportional to declared wire lengths.
internal static class CborScanner
{
    private struct Frame
    {
        internal ulong Remaining;
        internal CborMajorType Type;
        internal bool Indefinite;
        internal bool OddMapItem;
    }

    internal static CborDecodeResult Scan<TBuffer>(ref TBuffer buffer, CborReaderOptions options)
        where TBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
        => Scan(ref buffer, options, out _);

    internal static CborDecodeResult Scan<TBuffer>(ref TBuffer buffer, CborReaderOptions options, out long items)
        where TBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
        => Scan(ref buffer, new CborScanLimits(options), out items);

    internal static CborDecodeResult Scan<TBuffer>(ref TBuffer buffer, in CborScanLimits options, out long items)
        where TBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
    {
        items = 0;
        var result = CborReadBufferExtensions.TryPeekHeader(ref buffer, out var header);
        if (result != CborDecodeResult.Success)
        {
            return result;
        }

        if (header.MajorType is CborMajorType.UnsignedInteger or CborMajorType.NegativeInteger or CborMajorType.Simple)
        {
            if (header.EncodedLength > options.MaxEncodedLength || options.MaxItems == 0)
            {
                return CborDecodeResult.LimitExceeded;
            }

            if (header.IsBreak)
            {
                return CborDecodeResult.InvalidData;
            }

            if (options.RequirePreferredEncoding && !IsPreferred(in header))
            {
                return CborDecodeResult.EncodingPolicyViolation;
            }

            buffer.Advance(header.EncodedLength);
            items = 1;
            return CborDecodeResult.Success;
        }

        return ScanContainer(ref buffer, in options, out items);
    }

    private static CborDecodeResult ScanContainer<TBuffer>(ref TBuffer buffer, in CborScanLimits options, out long items)
        where TBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
    {
        Frame[]? rented = null;
        Span<Frame> frames = options.MaxDepth <= 64
            ? stackalloc Frame[65]
            : (rented = ArrayPool<Frame>.Shared.Rent(options.MaxDepth + 1));
        try
        {
            return ScanCore(ref buffer, options, frames, out items);
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<Frame>.Shared.Return(rented);
            }
        }
    }

    private static CborDecodeResult ScanCore<TBuffer>(ref TBuffer buffer, in CborScanLimits options, scoped Span<Frame> frames, out long items)
        where TBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
    {
        frames[0] = new Frame { Remaining = 1, Type = CborMajorType.Array };
        int depth = 0;
        long start = buffer.BytesConsumed;
        items = 0;
        while (true)
        {
            ref Frame parent = ref frames[depth];
            if (!parent.Indefinite && parent.Remaining == 0)
            {
                if (depth == 0)
                {
                    return CborDecodeResult.Success;
                }

                depth--;
                continue;
            }

            long bytesLeft = options.MaxEncodedLength - (buffer.BytesConsumed - start);
            if (bytesLeft == 0)
            {
                return CborDecodeResult.LimitExceeded;
            }

            var result = CborReadBufferExtensions.TryPeekHeader(ref buffer, out var header);
            if (result != CborDecodeResult.Success)
            {
                return result;
            }

            if (header.EncodedLength > bytesLeft)
            {
                return CborDecodeResult.LimitExceeded;
            }

            if (header.IsBreak)
            {
                if (!parent.Indefinite || (parent.Type == CborMajorType.Map && parent.OddMapItem))
                {
                    return CborDecodeResult.InvalidData;
                }

                buffer.Advance(1);
                depth--;
                continue;
            }

            if (parent.Indefinite && parent.Type is CborMajorType.ByteString or CborMajorType.TextString &&
                (header.MajorType != parent.Type || header.IsIndefiniteLength))
            {
                return CborDecodeResult.InvalidData;
            }

            if (items == options.MaxItems)
            {
                return CborDecodeResult.LimitExceeded;
            }

            if ((header.IsIndefiniteLength && !options.AllowIndefiniteLength) ||
                (options.RequirePreferredEncoding && !IsPreferred(in header)))
            {
                return CborDecodeResult.EncodingPolicyViolation;
            }

            // Account for this child only after its header has been checked.
            items++;
            if (!parent.Indefinite)
            {
                parent.Remaining--;
            }

            if (parent.Type == CborMajorType.Map)
            {
                parent.OddMapItem = !parent.OddMapItem;
            }

            switch (header.MajorType)
            {
                case CborMajorType.UnsignedInteger:
                case CborMajorType.NegativeInteger:
                case CborMajorType.Simple:
                    buffer.Advance(header.EncodedLength);
                    break;
                case CborMajorType.ByteString:
                case CborMajorType.TextString:
                    if (header.IsIndefiniteLength)
                    {
                        result = Push(frames, ref depth, in header, options, 0);
                        if (result != CborDecodeResult.Success)
                        {
                            return result;
                        }

                        buffer.Advance(header.EncodedLength);
                        break;
                    }

                    if (header.Argument > (ulong)(bytesLeft - header.EncodedLength))
                    {
                        return CborDecodeResult.LimitExceeded;
                    }

                    if (header.Argument > (ulong)(buffer.BytesRemaining - header.EncodedLength))
                    {
                        return CborDecodeResult.NeedMoreData;
                    }

                    buffer.Advance(header.EncodedLength);
                    result = ReadPayload(ref buffer, header.Argument, header.MajorType == CborMajorType.TextString);
                    if (result != CborDecodeResult.Success)
                    {
                        return result;
                    }

                    break;
                case CborMajorType.Array:
                case CborMajorType.Map:
                case CborMajorType.Tag:
                    ulong children = header.MajorType == CborMajorType.Tag ? 1 : header.Argument;
                    if (!header.IsIndefiniteLength)
                    {
                        ulong remainingBudget = (ulong)(options.MaxItems - items);
                        if (header.MajorType == CborMajorType.Map)
                        {
                            // Bound before multiplying so UInt64.MaxValue cannot wrap.
                            if (children > remainingBudget / 2)
                            {
                                return CborDecodeResult.LimitExceeded;
                            }

                            children *= 2;
                        }
                        else if (children > remainingBudget)
                        {
                            return CborDecodeResult.LimitExceeded;
                        }
                    }

                    result = Push(frames, ref depth, in header, options, children);
                    if (result != CborDecodeResult.Success)
                    {
                        return result;
                    }

                    buffer.Advance(header.EncodedLength);
                    break;
            }
        }
    }

    private static CborDecodeResult Push(Span<Frame> frames, ref int depth, in CborHeader header,
        in CborScanLimits options, ulong children)
    {
        if (depth == options.MaxDepth)
        {
            return CborDecodeResult.LimitExceeded;
        }

        frames[++depth] = new Frame
        {
            Type = header.MajorType,
            Remaining = children,
            Indefinite = header.IsIndefiniteLength,
        };
        return CborDecodeResult.Success;
    }

    internal static bool IsPreferred(in CborHeader header)
    {
        if (header.IsIndefiniteLength)
        {
            // Preferred serialization itself does not prohibit indefinite-length containers.
            return true;
        }

        if (header.MajorType != CborMajorType.Simple)
        {
            return header.EncodedLength == CborPrimitives.GetHeaderLength(header.Argument);
        }

        return header.AdditionalInformation < CborEncoding.Float16Argument || FloatEncoding.IsPreferred(in header);
    }

    private static CborDecodeResult ReadPayload<TBuffer>(ref TBuffer buffer, ulong length, bool text)
        where TBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
    {
        var utf8 = new Utf8Validator();
        while (length != 0)
        {
            ReadOnlySpan<byte> span = buffer.GetUnreadSpan();
            int count = (int)Math.Min((ulong)span.Length, length);
            if (count == 0)
            {
                return CborDecodeResult.NeedMoreData;
            }

            if (text && !utf8.Append(span.Slice(0, count)))
            {
                return CborDecodeResult.InvalidData;
            }

            buffer.Advance(count);
            length -= (uint)count;
        }

        return !text || utf8.IsComplete ? CborDecodeResult.Success : CborDecodeResult.InvalidData;
    }
}
