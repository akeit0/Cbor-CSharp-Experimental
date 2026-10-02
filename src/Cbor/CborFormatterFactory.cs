using SerializerFoundation;

namespace Cbor;

/// <summary>Creates uninitialized formatters for a buffer pair. Resolvers own initialization and publication.</summary>
/// <remarks>Return a fresh instance for each successful request. An initialized formatter belongs to one resolver graph and must not be shared between resolvers.</remarks>
public abstract class CborFormatterFactory
{
    /// <summary>The AOT-safe built-in scalar factory.</summary>
    public static CborFormatterFactory Builtin { get; } = new Internal.BuiltinFormatterFactory();

    /// <summary>Creates a fresh formatter for the requested buffer pair and value type, or returns null.</summary>
    public abstract object? CreateFormatter<TWriteBuffer, TReadBuffer>(Type valueType)
        where TWriteBuffer : struct, IWriteBuffer
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
        where TReadBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
    ;

    /// <summary>Creates a resolver with an independent cache of initialized formatter graphs.</summary>
    public CborFormatterResolver CreateResolver() => new(this);

    /// <summary>Snapshots and flattens a factory chain. The first factory serving the requested buffer pair and value type wins.</summary>
    public static CborFormatterFactory Combine(params CborFormatterFactory[] factories)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(factories);
#else
        if (factories is null) { throw new ArgumentNullException(nameof(factories)); }
#endif
        var copy = (CborFormatterFactory[])factories.Clone();
        if (Array.Exists(copy, static factory => factory is null))
        {
            throw new ArgumentException("Factories cannot contain null.", nameof(factories));
        }
        if (copy.Length == 0) { throw new ArgumentException("At least one factory is required.", nameof(factories)); }
        if (copy.Length == 1) { return copy[0]; }
        var flattened = new List<CborFormatterFactory>(copy.Length);
        foreach (var factory in copy)
        {
            if (factory is CompositeFactory composite) { flattened.AddRange(composite.Factories); }
            else { flattened.Add(factory); }
        }
        return new CompositeFactory(flattened.ToArray());
    }

    private sealed class CompositeFactory(CborFormatterFactory[] factories) : CborFormatterFactory
    {
        internal CborFormatterFactory[] Factories => factories;
        public override object? CreateFormatter<TWriteBuffer, TReadBuffer>(Type valueType)
        {
            foreach (var factory in factories)
            {
                var formatter = factory.CreateFormatter<TWriteBuffer, TReadBuffer>(valueType);
                if (formatter is not null) { return formatter; }
            }
            return null;
        }
    }
}
