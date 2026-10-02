namespace Cbor;

/// <summary>Immutable formatter selection and resource policy for typed serialization.</summary>
public sealed class CborSerializerOptions
{
    private const int DefaultMaxCollectionLength = 1_000_000;
    private const int DefaultMaxStringLength = 16 * 1024 * 1024;
    /// <summary>Built-in types with bounded reads and writes.</summary>
    public static CborSerializerOptions Default { get; } = new();

    /// <summary>Creates options with a factory chain and immutable resource limits. Custom factories precede built-ins.</summary>
    public CborSerializerOptions(CborFormatterFactory? factory = null, CborReaderOptions? readerOptions = null,
        int maxCollectionLength = DefaultMaxCollectionLength, int maxStringLength = DefaultMaxStringLength)
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

        Resolver = (factory is null ? CborFormatterFactory.Builtin :
            CborFormatterFactory.Combine(factory, CborFormatterFactory.Builtin)).CreateResolver();
        ReaderOptions = readerOptions ?? CborReaderOptions.Default;
        MaxCollectionLength = maxCollectionLength;
        MaxStringLength = maxStringLength;
    }

    /// <summary>Resolver shared by all nested values in an operation.</summary>
    public CborFormatterResolver Resolver { get; }

    /// <summary>Limits and CBOR encoding policy, also used while skipping unknown members.</summary>
    public CborReaderOptions ReaderOptions { get; }

    /// <summary>Maximum array elements or map pairs allocated or traversed by a typed formatter.</summary>
    public int MaxCollectionLength { get; }

    /// <summary>Maximum byte-string bytes or text-string UTF-8 bytes, including all indefinite chunks.</summary>
    public int MaxStringLength { get; }

}
