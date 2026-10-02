using SerializerFoundation;

namespace Cbor;

/// <summary>One tag number and its typed content. Nested wrappers represent a tag chain.</summary>
/// <remarks>This wrapper preserves tags without imposing a registered tag's semantic contract.</remarks>
/// <typeparam name="T">Tagged content type.</typeparam>
public readonly struct CborTagged<T>(ulong tag, T value) : IEquatable<CborTagged<T>>
{
    /// <summary>The unsigned tag number.</summary>
    public ulong Tag { get; } = tag;
    /// <summary>The tagged item.</summary>
    public T Value { get; } = value;
    /// <inheritdoc />
    public bool Equals(CborTagged<T> other) => Tag == other.Tag && EqualityComparer<T>.Default.Equals(Value, other.Value);
    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is CborTagged<T> other && Equals(other);
    /// <inheritdoc />
    public override int GetHashCode() => Tag.GetHashCode() ^ (Value is null ? 0 : EqualityComparer<T>.Default.GetHashCode(Value));
    /// <summary>Compares tag numbers and content values.</summary>
    public static bool operator ==(CborTagged<T> left, CborTagged<T> right) => left.Equals(right);
    /// <summary>Compares tag numbers and content values.</summary>
    public static bool operator !=(CborTagged<T> left, CborTagged<T> right) => !left.Equals(right);
}

/// <summary>Formats a tag and exactly one child, sharing the operation's resolver and budgets.</summary>
/// <typeparam name="T">Tagged content type.</typeparam>
/// <typeparam name="TWriteBuffer">Write buffer type.</typeparam>
/// <typeparam name="TReadBuffer">Read buffer type.</typeparam>
public sealed class CborTaggedFormatter<TWriteBuffer, TReadBuffer, T> :
    ICborFormatter<TWriteBuffer, TReadBuffer, CborTagged<T>>
    where TWriteBuffer : struct, IWriteBuffer
#if NET9_0_OR_GREATER
    , allows ref struct
#endif
    where TReadBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
    , allows ref struct
#endif
{
    /// <inheritdoc />
    private ICborFormatter<TWriteBuffer, TReadBuffer, T> formatter = null!;
    /// <inheritdoc />
    public void Initialize(CborFormatterResolver resolver) => formatter = resolver.GetFormatter<TWriteBuffer, TReadBuffer, T>();
    /// <inheritdoc />
    public void Serialize(ref TWriteBuffer buffer, ref CborSerializationContext context, CborTagged<T> value)
    {
        context.EnterContainer();
        try
        {
            context.CheckEncodedLength(buffer.BytesWritten, CborPrimitives.GetHeaderLength(value.Tag));
            buffer.WriteTag(value.Tag);
            context.Serialize(ref buffer, value.Value, formatter);
        }
        finally
        {
            context.ExitContainer();
        }
    }

    /// <inheritdoc />
    public CborTagged<T> Deserialize(ref TReadBuffer buffer, ref CborDeserializationContext context)
    {
        context.EnterContainer();
        try
        {
            ulong tag = buffer.ReadTag();
            return new(tag, context.Deserialize(ref buffer, formatter));
        }
        finally
        {
            context.ExitContainer();
        }
    }
}
