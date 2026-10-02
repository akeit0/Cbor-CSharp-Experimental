namespace Cbor;

/// <summary>Immutable resource limits and syntax policy for structural validation and skipping.</summary>
/// <remarks>This is not a semantic tag validator or a deterministic map-key profile.</remarks>
public sealed class CborReaderOptions
{
    /// <summary>Conservative defaults: 64 nesting levels, one million tokens, and 16 MiB per item.</summary>
    public static CborReaderOptions Default { get; } = new();

    /// <summary>Creates options. Depth counts containers and tags, including empty containers; the root scalar has depth zero.</summary>
    public CborReaderOptions(int maxDepth = 64, long maxItems = 1_000_000, long maxEncodedLength = 16 * 1024 * 1024,
        bool allowIndefiniteLength = true, bool requirePreferredEncoding = false)
    {
        if (maxDepth is < 0 or > 1024)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDepth));
        }

#if NET9_0_OR_GREATER
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxItems);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxEncodedLength);
#else
        if (maxItems <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxItems));
        }

        if (maxEncodedLength <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxEncodedLength));
        }
#endif

        MaxDepth = maxDepth;
        MaxItems = maxItems;
        MaxEncodedLength = maxEncodedLength;
        AllowIndefiniteLength = allowIndefiniteLength;
        RequirePreferredEncoding = requirePreferredEncoding;
    }

    /// <summary>Maximum simultaneously nested containers/tags. At most 1024 can be configured.</summary>
    public int MaxDepth { get; }

    /// <summary>Maximum tokens per item, including tags and string chunks but excluding break markers.</summary>
    public long MaxItems { get; }

    /// <summary>Maximum encoded byte length of one complete item.</summary>
    public long MaxEncodedLength { get; }

    /// <summary>Whether indefinite-length strings, arrays, and maps are accepted.</summary>
    public bool AllowIndefiniteLength { get; }

    /// <summary>Requires shortest integer/length/tag/float widths and canonical half NaN; does not sort or deduplicate maps.</summary>
    public bool RequirePreferredEncoding { get; }
}
