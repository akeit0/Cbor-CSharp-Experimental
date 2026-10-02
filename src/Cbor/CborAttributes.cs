namespace Cbor;

/// <summary>Opts a class or struct into explicit-key, generated CBOR map serialization.</summary>
/// <remarks>Closed generic models are supported. Model base classes must also opt in; their keyed slots form one map with the derived contract.</remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class CborObjectAttribute : Attribute;

/// <summary>Assigns a stable, nonnegative integer wire key to a property or field.</summary>
/// <remarks>Keys identify members across versions and must be unique throughout a model hierarchy. Virtual overrides preserve their slot's key and Required setting. Removing a member does not free its key for reuse.</remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = false)]
public sealed class CborKeyAttribute : Attribute
{
    /// <summary>Creates an explicit member key.</summary>
    public CborKeyAttribute(int key)
    {
        Key = key;
    }

    /// <summary>The CBOR map's integer key.</summary>
    public int Key { get; }

    /// <summary>Whether reading an object without this key must fail.</summary>
    public bool Required { get; set; }
}

/// <summary>Explicitly excludes a public instance member from the wire contract.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = false)]
public sealed class CborIgnoreAttribute : Attribute;

/// <summary>Selects the accessible constructor used by a generated object formatter.</summary>
/// <remarks>Parameters must match keyed members by name (case insensitive) and CLR type.</remarks>
[AttributeUsage(AttributeTargets.Constructor, Inherited = false)]
public sealed class CborConstructorAttribute : Attribute;

/// <summary>Generates an AOT-safe factory for the specified closed types and their reachable contracts.</summary>
/// <remarks>Apply to a top-level, nongeneric partial class. Pass roots explicitly, then use the generated Instance property.</remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class CborFactoryAttribute : Attribute
{
    /// <summary>Defines the model and collection roots of a generated factory.</summary>
    public CborFactoryAttribute(params Type[] types)
    {
        Types = types;
    }

    /// <summary>Root types included in the generated factory.</summary>
    public Type[] Types { get; }
}
