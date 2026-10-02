using SerializerFoundation;

namespace Cbor;

/// <summary>Creates uninitialized formatters for a buffer pair. Resolvers own initialization and publication.</summary>
/// <remarks>Return a fresh instance for each successful request. An initialized formatter belongs to one resolver graph and must not be shared between resolvers.</remarks>
public abstract class CborFormatterFactory
{
    /// <summary>The AOT-safe built-in scalar factory.</summary>
    public static CborFormatterFactory Builtin { get; } = new Internal.BuiltinFormatterFactory();

#if NET9_0_OR_GREATER
    /// <summary>Creates a formatter for arbitrary modern buffer types; downlevel providers use the Type overload.</summary>
    public virtual object? CreateFormatter<TWriteBuffer, TReadBuffer>(Type valueType)
        where TWriteBuffer : struct, IWriteBuffer, allows ref struct
        where TReadBuffer : struct, IReadBuffer, allows ref struct
        => CreateFormatter(typeof(TWriteBuffer), typeof(TReadBuffer), valueType);
#endif

    /// <summary>Creates a formatter for a supported buffer pair, or returns null. This signature is stable across targets.</summary>
    public abstract object? CreateFormatter(Type writeBufferType, Type readBufferType, Type valueType);

    /// <summary>Creates a resolver with an independent cache of initialized formatter graphs.</summary>
    public CborFormatterResolver CreateResolver() => new(this);

    /// <summary>Adapts a generated, built-in, or factory-backed resolver without reusing its initialized graph.</summary>
    public static CborFormatterFactory FromResolver(CborFormatterResolver resolver)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(resolver);
#else
        if (resolver is null) { throw new ArgumentNullException(nameof(resolver)); }
#endif
        if (!resolver.HasFactoryProvider)
        {
            throw new ArgumentException("Single-type providers must be composed with CborCompositeResolver; factory composition requires a buffer-pair provider.", nameof(resolver));
        }
        return new ResolverFactory(resolver);
    }

    /// <summary>Copies a factory chain. The first factory serving a type wins.</summary>
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
        return new CompositeFactory(copy);
    }

    private sealed class ResolverFactory(CborFormatterResolver resolver) : CborFormatterFactory
    {
        public override object? CreateFormatter(Type writeBufferType, Type readBufferType, Type valueType)
            => resolver.CreateProvidedFormatter(writeBufferType, readBufferType, valueType);
#if NET9_0_OR_GREATER
        public override object? CreateFormatter<TWriteBuffer, TReadBuffer>(Type valueType)
            => resolver.CreateProvidedFormatter<TWriteBuffer, TReadBuffer>(valueType);
#endif
    }

    private sealed class CompositeFactory(CborFormatterFactory[] factories) : CborFormatterFactory
    {
        public override object? CreateFormatter(Type writeBufferType, Type readBufferType, Type valueType)
        {
            foreach (var factory in factories)
            {
                var formatter = factory.CreateFormatter(writeBufferType, readBufferType, valueType);
                if (formatter is not null) { return formatter; }
            }
            return null;
        }
#if NET9_0_OR_GREATER
        public override object? CreateFormatter<TWriteBuffer, TReadBuffer>(Type valueType)
        {
            foreach (var factory in factories)
            {
                var formatter = factory.CreateFormatter<TWriteBuffer, TReadBuffer>(valueType);
                if (formatter is not null) { return formatter; }
                // A downlevel customization must keep precedence over later modern built-ins.
#pragma warning disable CA2263 // Probe the stable Type overload implemented by downlevel providers.
                if ((typeof(TWriteBuffer).IsByRefLike || typeof(TReadBuffer).IsByRefLike) &&
                    factory.CreateFormatter(typeof(CompatibleArrayPoolListWriteBuffer), typeof(CompatibleReadOnlySpanReadBuffer), valueType) is not null)
                {
                    return Internal.CompatiblePairRequiredProvider.Instance;
                }
#pragma warning restore CA2263
            }
            return null;
        }
#endif
    }
}
