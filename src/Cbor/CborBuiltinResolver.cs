namespace Cbor;
/// <summary>AOT-safe built-in scalar resolver. Each consumer resolver constructs its own initialized graph.</summary>
public sealed class CborBuiltinResolver : CborFormatterResolver
{
    /// <summary>Shared built-in resolver.</summary>
    public static CborBuiltinResolver Instance { get; } = new();
    private CborBuiltinResolver() { }
    /// <inheritdoc />
    protected override CborFormatterFactory Provider => CborFormatterFactory.Builtin;
}
