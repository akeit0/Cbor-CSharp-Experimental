using Cbor.Internal;
using System.Runtime.CompilerServices;
using SerializerFoundation;

namespace Cbor;

/// <summary>Shared read budgets and formatter resolution, including unknown-member traversal.</summary>
/// <remarks>Single-owner mutable state: pass by ref, never copy or box, and dispose in finally.</remarks>
public struct CborDeserializationContext : IDisposable
{
    private readonly CborSerializerOptions options;
    private int depth;
    private long items;
    private long declaredElements;

    /// <summary>Creates context for a bounded message; declared child counts cannot exceed its bytes.</summary>
    public CborDeserializationContext(CborSerializerOptions options, long messageLength)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        if (messageLength < 0 || messageLength > options.ReaderOptions.MaxEncodedLength)
        {
            throw new InvalidDataException("The CBOR encoded byte budget was exceeded.");
        }

        declaredElements = messageLength;
        depth = 0;
        items = 0;
    }

    /// <summary>Completes the operation. Contexts retain no pooled formatter storage.</summary>
    public readonly void Dispose() { }

    /// <summary>Immutable options for this operation.</summary>
    public readonly CborSerializerOptions Options => options;

    /// <summary>Reads a child through its initialized formatter, charging and checking one item.</summary>
    public T Deserialize<TWriteBuffer, TReadBuffer, T>(ref TReadBuffer buffer, ICborFormatter<TWriteBuffer, TReadBuffer, T> formatter)
        where TWriteBuffer : struct, IWriteBuffer
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
        where TReadBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
    {
        ChargeItem();
        CheckNextValue(ref buffer);
        return DeserializeSameItem(ref buffer, formatter);
    }

    /// <summary>Delegates the current item without charging or checking a second wire token.</summary>
    public T DeserializeSameItem<TWriteBuffer, TReadBuffer, T>(ref TReadBuffer buffer, ICborFormatter<TWriteBuffer, TReadBuffer, T> formatter)
        where TWriteBuffer : struct, IWriteBuffer
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
        where TReadBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(formatter);
#else
        if (formatter is null) { throw new ArgumentNullException(nameof(formatter)); }
#endif
        return formatter.Deserialize(ref buffer, ref this);
    }

    /// <summary>Reads a contract key without applying CLR integer overrides; other key shapes are skipped and return -1.</summary>
    public int ReadObjectKey<TReadBuffer>(ref TReadBuffer buffer)
        where TReadBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
            , allows ref struct
#endif
    {
        var span = buffer.GetUnreadSpan();
        if (!span.IsEmpty && span[0] < CborEncoding.DirectArgumentLimit)
        {
            int key = span[0];
            ChargeItem();
            buffer.Advance(CborEncoding.ImmediateHeaderLength);
            return key;
        }

        var header = buffer.PeekHeader();
        if (header.MajorType != CborMajorType.UnsignedInteger || header.Argument > int.MaxValue)
        {
            SkipValue(ref buffer);
            return -1;
        }

        ChargeItem();
        CheckHeader(header);
        buffer.Advance(header.EncodedLength);
        return (int)header.Argument;
    }

    /// <summary>Enters a container. The zero-depth policy permits only scalars.</summary>
    public void EnterContainer()
    {
        if (depth == options.ReaderOptions.MaxDepth)
        {
            throw new InvalidDataException("The CBOR nesting depth was exceeded.");
        }

        depth++;
    }

    /// <summary>Leaves a container; call from finally.</summary>
    public void ExitContainer() => depth--;

    /// <summary>Reads an array header and bounds declared allocations before any memory request.</summary>
    public int? ReadArrayLength<TReadBuffer>(ref TReadBuffer buffer)
        where TReadBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
            , allows ref struct
#endif
        => ReadCollectionLength(ref buffer, false);

    /// <summary>Reads a map header and bounds pair counts before multiplying or allocating.</summary>
    public int? ReadMapLength<TReadBuffer>(ref TReadBuffer buffer)
        where TReadBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
            , allows ref struct
#endif
        => ReadCollectionLength(ref buffer, true);

    /// <summary>Bounds growth of an indefinite collection or an object member loop.</summary>
    public readonly void CheckCollectionLength(int count)
    {
        if ((uint)count > (uint)options.MaxCollectionLength)
        {
            throw new InvalidDataException("The CBOR collection length limit was exceeded.");
        }
    }

    /// <summary>Consumes null if present. Undefined is a distinct value and is not accepted as null.</summary>
    public static bool TryReadNull<TReadBuffer>(ref TReadBuffer buffer)
        where TReadBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
            , allows ref struct
#endif
    {
        var span = buffer.GetUnreadSpan();
        if (span.IsEmpty || span[0] != CborEncoding.Null)
        {
            return false;
        }

        buffer.Advance(CborEncoding.ImmediateHeaderLength);
        return true;
    }

    /// <summary>Skips a version-unknown value, sharing remaining depth, item, and encoding policies.</summary>
    public void SkipValue<TReadBuffer>(ref TReadBuffer buffer)
        where TReadBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
            , allows ref struct
#endif
    {
        if (buffer.BytesRemaining == 0)
        {
            CborError.Throw(CborDecodeResult.NeedMoreData);
        }

        if (items >= options.ReaderOptions.MaxItems)
        {
            throw new InvalidDataException("The CBOR item budget was exceeded.");
        }

        var policy = options.ReaderOptions;
        var remaining = new CborScanLimits(policy.MaxDepth - depth, policy.MaxItems - items,
            Math.Min(policy.MaxEncodedLength, buffer.BytesRemaining), policy.AllowIndefiniteLength,
            policy.RequirePreferredEncoding);
        var result = CborScanner.Scan(ref buffer, remaining, out long skippedItems);
        items += skippedItems;
        if (result != CborDecodeResult.Success)
        {
            CborError.Throw(result);
        }
    }

    internal void ChargeItem()
    {
        if (items >= options.ReaderOptions.MaxItems)
        {
            throw new InvalidDataException("The CBOR item budget was exceeded.");
        }

        items++;
    }

    internal readonly void CheckHeader(CborHeader header)
    {
        if (header.IsBreak)
        {
            throw new InvalidDataException("An unexpected CBOR break marker was encountered.");
        }

        var policy = options.ReaderOptions;
        if ((header.IsIndefiniteLength && !policy.AllowIndefiniteLength) ||
            (policy.RequirePreferredEncoding && !CborScanner.IsPreferred(in header)))
        {
            throw new InvalidDataException("The CBOR encoding policy was violated.");
        }
    }

    private readonly void CheckNextValue<TReadBuffer>(ref TReadBuffer buffer)
        where TReadBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
            , allows ref struct
#endif
    {
        var span = buffer.GetUnreadSpan();
        if (!span.IsEmpty && (span[0] & CborEncoding.AdditionalInformationMask) < CborEncoding.DirectArgumentLimit)
        {
            return; // Every immediate argument is structurally valid and preferred.
        }

        CheckNextValueCold(ref buffer);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private readonly void CheckNextValueCold<TReadBuffer>(ref TReadBuffer buffer)
        where TReadBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
            , allows ref struct
#endif
    {
        var span = CborReadBufferExtensions.GetTokenSpan(ref buffer);
        if (span.IsEmpty)
        {
            CborError.Throw(CborDecodeResult.NeedMoreData);
        }
        var result = CborTokenDecoder.ValidateExtendedPrefix(span);
        if (result != CborDecodeResult.Success)
        {
            CborError.Throw(result);
        }
        if ((span[0] & CborEncoding.AdditionalInformationMask) == CborEncoding.IndefiniteArgument && !options.ReaderOptions.AllowIndefiniteLength)
        {
            CborError.Throw(CborDecodeResult.EncodingPolicyViolation);
        }

        if (options.ReaderOptions.RequirePreferredEncoding)
        {
            CheckHeader(buffer.PeekHeader());
        }
    }

    private int? ReadCollectionLength<TReadBuffer>(ref TReadBuffer buffer, bool map)
        where TReadBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
            , allows ref struct
#endif
    {
        var count = map ? buffer.ReadMapHeader() : buffer.ReadArrayHeader();
        if (!count.HasValue)
        {
            return null;
        }

        int multiplier = map ? 2 : 1;
        if (count.Value > (ulong)options.MaxCollectionLength ||
            count.Value > (ulong)(declaredElements / multiplier) ||
            count.Value > (ulong)(buffer.BytesRemaining / multiplier) ||
            count.Value > (ulong)((options.ReaderOptions.MaxItems - items) / multiplier))
        {
            throw new InvalidDataException("The declared CBOR collection length exceeds the remaining budget.");
        }

        declaredElements -= (long)count.Value * multiplier;
        return (int)count.Value;
    }
}
