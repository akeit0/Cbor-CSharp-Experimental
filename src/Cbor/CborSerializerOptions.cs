namespace Cbor;

/// <summary>Immutable formatter selection and resource policy for typed serialization.</summary>
public sealed class CborSerializerOptions
{
    /// <summary>Built-in types with bounded reads and writes.</summary>
    public static CborSerializerOptions Default { get; } = new();

    /// <summary>Creates options. A supplied resolver takes precedence over built-in scalar formatters.</summary>
    public CborSerializerOptions(CborFormatterResolver? resolver = null, CborReaderOptions? readerOptions = null,
        int maxCollectionLength = 1_000_000, int maxStringLength = 16 * 1024 * 1024)
        : this(false, resolver, readerOptions, maxCollectionLength, maxStringLength)
    {
    }

    /// <summary>Creates options with an explicit compatible-buffer preference, retaining immutable resolver and limits.</summary>
    public CborSerializerOptions(bool useCompatibleBuffers, CborFormatterResolver? resolver = null, CborReaderOptions? readerOptions = null,
        int maxCollectionLength = 1_000_000, int maxStringLength = 16 * 1024 * 1024)
    {
#if NET8_0_OR_GREATER
        ArgumentOutOfRangeException.ThrowIfNegative(maxCollectionLength);
        ArgumentOutOfRangeException.ThrowIfNegative(maxStringLength);
#else
        if (maxCollectionLength < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxCollectionLength));
        }

        if (maxStringLength < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxStringLength));
        }
#endif

        Resolver = resolver is null ? CborBuiltinResolver.Instance :
            new CborCompositeResolver(resolver, CborBuiltinResolver.Instance);
        ReaderOptions = readerOptions ?? CborReaderOptions.Default;
        MaxCollectionLength = maxCollectionLength;
        MaxStringLength = maxStringLength;
        UseCompatibleBuffers = useCompatibleBuffers;
    }

    /// <summary>Resolver shared by all nested values in an operation.</summary>
    public CborFormatterResolver Resolver { get; }

    /// <summary>Limits and CBOR encoding policy, also used while skipping unknown members.</summary>
    public CborReaderOptions ReaderOptions { get; }

    /// <summary>Maximum array elements or map pairs allocated or traversed by a typed formatter.</summary>
    public int MaxCollectionLength { get; }

    /// <summary>Maximum byte-string bytes or text-string UTF-8 bytes, including all indefinite chunks.</summary>
    public int MaxStringLength { get; }

    /// <summary>Uses ordinary struct buffers so formatters compiled for older targets remain usable on modern runtimes.</summary>
    public bool UseCompatibleBuffers { get; }
}
