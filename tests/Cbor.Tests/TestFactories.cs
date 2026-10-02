using SerializerFoundation;

namespace Cbor.Tests;

internal abstract class TestFactory : CborFormatterFactory
{
    public sealed override object? CreateFormatter<W, R>(Type valueType) => Create<W, R>(valueType);

    protected abstract object? Create<W, R>(Type valueType)
        where W : struct, IWriteBuffer
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
        where R : struct, IReadBuffer
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
    ;

    protected static object? From<W, R>(CborFormatterFactory factory, Type valueType)
        where W : struct, IWriteBuffer
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
        where R : struct, IReadBuffer
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
        => factory.CreateFormatter<W, R>(valueType);
}

internal sealed class OffsetFactory(int offset) : TestFactory
{
    private readonly int shift = offset;
    private int reads;
    internal int Reads => reads;
    protected override object? Create<W, R>(Type valueType) => valueType == typeof(int) ? new Formatter<W, R>(this) : null;

    private sealed class Formatter<W, R>(OffsetFactory owner) : ICborFormatter<W, R, int>
        where W : struct, IWriteBuffer
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
        where R : struct, IReadBuffer
#if NET9_0_OR_GREATER
        , allows ref struct
#endif
    {
        public void Initialize(CborFormatterResolver resolver) { }
        public void Serialize(ref W buffer, ref CborSerializationContext context, int value) => buffer.WriteInt64(checked(value + owner.shift));
        public int Deserialize(ref R buffer, ref CborDeserializationContext context)
        {
            Interlocked.Increment(ref owner.reads);
            return checked((int)buffer.ReadInt64() - owner.shift);
        }
    }
}

internal sealed class DictionaryFactory<TKey, TValue>(IEqualityComparer<TKey>? comparer = null) : TestFactory where TKey : notnull
{
    protected override object? Create<W, R>(Type valueType) => valueType == typeof(Dictionary<TKey, TValue>) ?
        new CborDictionaryFormatter<W, R, TKey, TValue>(comparer) : null;
}
