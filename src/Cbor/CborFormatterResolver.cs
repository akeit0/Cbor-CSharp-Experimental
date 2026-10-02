namespace Cbor;

/// <summary>Resolves immutable formatters without runtime code generation or assembly scanning.</summary>
public abstract class CborFormatterResolver
{
    /// <summary>Returns the formatter for T, or null when the type is unsupported.</summary>
    public abstract ICborFormatter<T>? GetFormatter<T>();

    /// <summary>Returns a formatter or throws for an unsupported type.</summary>
    public ICborFormatter<T> GetRequiredFormatter<T>() => GetFormatter<T>() ??
        throw new NotSupportedException("No CBOR formatter is registered for " + typeof(T).FullName + ".");
}

/// <summary>An ordered, immutable resolver chain. The first formatter wins.</summary>
public sealed class CborCompositeResolver : CborFormatterResolver
{
    private readonly CborFormatterResolver[] resolvers;

    /// <summary>Copies the resolver sequence so later caller mutations cannot change selection.</summary>
    public CborCompositeResolver(params CborFormatterResolver[] resolvers)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(resolvers);
#else
        if (resolvers is null)
        {
            throw new ArgumentNullException(nameof(resolvers));
        }
#endif

        this.resolvers = (CborFormatterResolver[])resolvers.Clone();
        if (Array.Exists(this.resolvers, static resolver => resolver is null))
        {
            throw new ArgumentException("Resolvers cannot contain null.", nameof(resolvers));
        }
    }

    /// <inheritdoc />
    public override ICborFormatter<T>? GetFormatter<T>()
    {
        foreach (var resolver in resolvers)
        {
            var formatter = resolver.GetFormatter<T>();
            if (formatter is not null)
            {
                return formatter;
            }
        }

        return null;
    }
}

/// <summary>Builds an explicit type registry. Build takes an immutable snapshot.</summary>
public sealed class CborFormatterRegistry
{
    private readonly Dictionary<Type, object> formatters = new();

    /// <summary>Registers a thread-safe formatter. Duplicate registrations are rejected.</summary>
    public CborFormatterRegistry Add<T>(ICborFormatter<T> formatter)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(formatter);
#else
        if (formatter is null)
        {
            throw new ArgumentNullException(nameof(formatter));
        }
#endif

        if (formatters.ContainsKey(typeof(T)))
        {
            throw new ArgumentException("A formatter is already registered for " + typeof(T).FullName + ".", nameof(formatter));
        }

        formatters.Add(typeof(T), formatter);
        return this;
    }

    /// <summary>Freezes the current registrations; the builder can then be reused independently.</summary>
    public CborFormatterResolver Build() => new RegistryResolver(new Dictionary<Type, object>(formatters));

    private sealed class RegistryResolver(Dictionary<Type, object> formatters) : CborFormatterResolver
    {
        public override ICborFormatter<T>? GetFormatter<T>() =>
            formatters.TryGetValue(typeof(T), out var formatter) ? (ICborFormatter<T>)formatter : null;
    }
}
