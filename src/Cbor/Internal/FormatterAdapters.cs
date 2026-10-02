using SerializerFoundation;
namespace Cbor.Internal;

internal interface IFactoryFormatter
{
    CborFormatterResolver Resolver { get; }
}

internal sealed class FactoryFormatter<T>(CborFormatterResolver resolver) : ICborFormatter<T>, IFactoryFormatter
{
    public CborFormatterResolver Resolver => resolver;
    public void Serialize<TWriteBuffer>(ref TWriteBuffer buffer, ref CborSerializationContext context, T value)
        where TWriteBuffer : struct, IWriteBuffer
        => context.SerializeSameItem(ref buffer, value, context.Options.Resolver.GetFormatter<TWriteBuffer, CompatibleReadOnlySpanReadBuffer, T>());
    public T Deserialize<TReadBuffer>(ref TReadBuffer buffer, ref CborDeserializationContext context)
        where TReadBuffer : struct, IReadBuffer
        => context.DeserializeSameItem(ref buffer, context.Options.Resolver.GetFormatter<CompatibleArrayPoolListWriteBuffer, TReadBuffer, T>());
}

internal static class LegacyFormatterAdapter
{
    internal static object? Create<TWriteBuffer, TReadBuffer, T>(ICborFormatter<T> formatter)
        where TWriteBuffer : struct, IWriteBuffer
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
        where TReadBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
    {
        if (typeof(TWriteBuffer) == typeof(CompatibleArrayPoolListWriteBuffer) && typeof(TReadBuffer) == typeof(CompatibleReadOnlySpanReadBuffer)) { return new LegacyFormatterAdapter<CompatibleArrayPoolListWriteBuffer, CompatibleReadOnlySpanReadBuffer, T>(formatter); }
        if (typeof(TWriteBuffer) == typeof(CompatibleArrayPoolListWriteBuffer) && typeof(TReadBuffer) == typeof(CompatibleReadOnlySequenceReadBuffer)) { return new LegacyFormatterAdapter<CompatibleArrayPoolListWriteBuffer, CompatibleReadOnlySequenceReadBuffer, T>(formatter); }
        if (typeof(TWriteBuffer) == typeof(CompatibleBufferWriterWriteBuffer) && typeof(TReadBuffer) == typeof(CompatibleReadOnlySpanReadBuffer)) { return new LegacyFormatterAdapter<CompatibleBufferWriterWriteBuffer, CompatibleReadOnlySpanReadBuffer, T>(formatter); }
        if (typeof(TWriteBuffer) == typeof(CompatibleBufferWriterWriteBuffer) && typeof(TReadBuffer) == typeof(CompatibleReadOnlySequenceReadBuffer)) { return new LegacyFormatterAdapter<CompatibleBufferWriterWriteBuffer, CompatibleReadOnlySequenceReadBuffer, T>(formatter); }
        return null;
    }
}
internal sealed class LegacyFormatterAdapter<TWriteBuffer, TReadBuffer, T>(ICborFormatter<T> formatter) : ICborFormatter<TWriteBuffer, TReadBuffer, T>
    where TWriteBuffer : struct, IWriteBuffer
    where TReadBuffer : struct, IReadBuffer
{
    public void Initialize(CborFormatterResolver resolver) { }
    public void Serialize(ref TWriteBuffer buffer, ref CborSerializationContext context, T value) => formatter.Serialize(ref buffer, ref context, value);
    public T Deserialize(ref TReadBuffer buffer, ref CborDeserializationContext context) => formatter.Deserialize(ref buffer, ref context);
}

internal interface ICompatiblePairRequired { }

internal sealed class CompatiblePairRequiredProvider
{
    internal static readonly CompatiblePairRequiredProvider Instance = new();
}

internal class MissingFormatter<TWriteBuffer, TReadBuffer, T> : ICborFormatter<TWriteBuffer, TReadBuffer, T>
    where TWriteBuffer : struct, IWriteBuffer
#if NET9_0_OR_GREATER
    , allows ref struct
#endif
    where TReadBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
    , allows ref struct
#endif
{
    public void Initialize(CborFormatterResolver resolver) { }
    public virtual void Serialize(ref TWriteBuffer buffer, ref CborSerializationContext context, T value) => throw Missing();
    public virtual T Deserialize(ref TReadBuffer buffer, ref CborDeserializationContext context) => throw Missing();
    private static NotSupportedException Missing() => new("No CBOR formatter is registered for " + typeof(T).FullName + " over the requested buffer pair.");
}

internal sealed class CompatiblePairRequired<TWriteBuffer, TReadBuffer, T> : MissingFormatter<TWriteBuffer, TReadBuffer, T>, ICompatiblePairRequired
    where TWriteBuffer : struct, IWriteBuffer
#if NET9_0_OR_GREATER
    , allows ref struct
#endif
    where TReadBuffer : struct, IReadBuffer
#if NET9_0_OR_GREATER
    , allows ref struct
#endif
{
    public override void Serialize(ref TWriteBuffer buffer, ref CborSerializationContext context, T value) => throw Required();
    public override T Deserialize(ref TReadBuffer buffer, ref CborDeserializationContext context) => throw Required();
    private static NotSupportedException Required() => new("The formatter graph requires compatible buffers. Use an owned serializer entry point or a compatible buffer pair.");
}
