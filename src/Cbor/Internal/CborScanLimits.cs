namespace Cbor.Internal;

// Operation-local scanner budgets stay on the stack; public options remain immutable reference types.
internal readonly struct CborScanLimits(int maxDepth, long maxItems, long maxEncodedLength,
    bool allowIndefiniteLength, bool requirePreferredEncoding)
{
    internal CborScanLimits(CborReaderOptions options)
        : this(options.MaxDepth, options.MaxItems, options.MaxEncodedLength, options.AllowIndefiniteLength, options.RequirePreferredEncoding)
    {
    }

    internal int MaxDepth { get; } = maxDepth;
    internal long MaxItems { get; } = maxItems;
    internal long MaxEncodedLength { get; } = maxEncodedLength;
    internal bool AllowIndefiniteLength { get; } = allowIndefiniteLength;
    internal bool RequirePreferredEncoding { get; } = requirePreferredEncoding;
}
