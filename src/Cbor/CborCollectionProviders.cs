using SerializerFoundation;
#if !NET9_0_OR_GREATER
// The downlevel helper mirrors the modern virtual factory signature.
#pragma warning disable CA1822, CA1859
#endif
using Cbor.Internal;
namespace Cbor;
/// <summary>Explicit collection provider. New factories create the buffer-specific formatter directly.</summary>
public sealed class CborArrayFormatter<T> : ICborFormatter<T[]?>, IFactoryFormatter
{
    private readonly CborFormatterResolver resolver;
    private readonly Factory factory;
    CborFormatterResolver IFactoryFormatter.Resolver => resolver;
    /// <summary>Creates an explicit collection provider.</summary>
    public CborArrayFormatter() { factory = new Factory(); resolver = factory.CreateResolver(); }
    /// <inheritdoc />
    public void Serialize<TWriteBuffer>(ref TWriteBuffer buffer, ref CborSerializationContext context, T[]? value) where TWriteBuffer : struct, IWriteBuffer
    {
        var formatter = (ICborFormatter<TWriteBuffer, CompatibleReadOnlySpanReadBuffer, T[]?>)factory.CreateFormatter<TWriteBuffer, CompatibleReadOnlySpanReadBuffer>(typeof(T[]))!;
        formatter.Initialize(context.Options.Resolver);
        context.SerializeSameItem(ref buffer, value, formatter);
    }
    /// <inheritdoc />
    public T[]? Deserialize<TReadBuffer>(ref TReadBuffer buffer, ref CborDeserializationContext context) where TReadBuffer : struct, IReadBuffer
    {
        var formatter = (ICborFormatter<CompatibleArrayPoolListWriteBuffer, TReadBuffer, T[]?>)factory.CreateFormatter<CompatibleArrayPoolListWriteBuffer, TReadBuffer>(typeof(T[]))!;
        formatter.Initialize(context.Options.Resolver);
        return context.DeserializeSameItem(ref buffer, formatter);
    }
    private sealed class Factory : CborFormatterFactory
    {
        public override object? CreateFormatter(Type writeBufferType, Type readBufferType, Type valueType)
        {
            if (writeBufferType == typeof(CompatibleArrayPoolListWriteBuffer) && readBufferType == typeof(CompatibleReadOnlySpanReadBuffer)) { return CreateFormatter<CompatibleArrayPoolListWriteBuffer, CompatibleReadOnlySpanReadBuffer>(valueType); }
            if (writeBufferType == typeof(CompatibleArrayPoolListWriteBuffer) && readBufferType == typeof(CompatibleReadOnlySequenceReadBuffer)) { return CreateFormatter<CompatibleArrayPoolListWriteBuffer, CompatibleReadOnlySequenceReadBuffer>(valueType); }
            if (writeBufferType == typeof(CompatibleBufferWriterWriteBuffer) && readBufferType == typeof(CompatibleReadOnlySpanReadBuffer)) { return CreateFormatter<CompatibleBufferWriterWriteBuffer, CompatibleReadOnlySpanReadBuffer>(valueType); }
            if (writeBufferType == typeof(CompatibleBufferWriterWriteBuffer) && readBufferType == typeof(CompatibleReadOnlySequenceReadBuffer)) { return CreateFormatter<CompatibleBufferWriterWriteBuffer, CompatibleReadOnlySequenceReadBuffer>(valueType); }
            return null;
        }
#if NET9_0_OR_GREATER
        public override
#else
        public
#endif
        object? CreateFormatter<TWriteBuffer, TReadBuffer>(Type valueType)
#if !NET9_0_OR_GREATER
            where TWriteBuffer : struct, IWriteBuffer
            where TReadBuffer : struct, IReadBuffer
#endif
            => valueType == typeof(T[]) ? new CborArrayFormatter<TWriteBuffer, TReadBuffer, T>() : null;
    }
}
/// <summary>Explicit collection provider. New factories create the buffer-specific formatter directly.</summary>
public sealed class CborListFormatter<T> : ICborFormatter<List<T>?>, IFactoryFormatter
{
    private readonly CborFormatterResolver resolver;
    private readonly Factory factory;
    CborFormatterResolver IFactoryFormatter.Resolver => resolver;
    /// <summary>Creates an explicit collection provider.</summary>
    public CborListFormatter() { factory = new Factory(); resolver = factory.CreateResolver(); }
    /// <inheritdoc />
    public void Serialize<TWriteBuffer>(ref TWriteBuffer buffer, ref CborSerializationContext context, List<T>? value) where TWriteBuffer : struct, IWriteBuffer
    {
        var formatter = (ICborFormatter<TWriteBuffer, CompatibleReadOnlySpanReadBuffer, List<T>?>)factory.CreateFormatter<TWriteBuffer, CompatibleReadOnlySpanReadBuffer>(typeof(List<T>))!;
        formatter.Initialize(context.Options.Resolver);
        context.SerializeSameItem(ref buffer, value, formatter);
    }
    /// <inheritdoc />
    public List<T>? Deserialize<TReadBuffer>(ref TReadBuffer buffer, ref CborDeserializationContext context) where TReadBuffer : struct, IReadBuffer
    {
        var formatter = (ICborFormatter<CompatibleArrayPoolListWriteBuffer, TReadBuffer, List<T>?>)factory.CreateFormatter<CompatibleArrayPoolListWriteBuffer, TReadBuffer>(typeof(List<T>))!;
        formatter.Initialize(context.Options.Resolver);
        return context.DeserializeSameItem(ref buffer, formatter);
    }
    private sealed class Factory : CborFormatterFactory
    {
        public override object? CreateFormatter(Type writeBufferType, Type readBufferType, Type valueType)
        {
            if (writeBufferType == typeof(CompatibleArrayPoolListWriteBuffer) && readBufferType == typeof(CompatibleReadOnlySpanReadBuffer)) { return CreateFormatter<CompatibleArrayPoolListWriteBuffer, CompatibleReadOnlySpanReadBuffer>(valueType); }
            if (writeBufferType == typeof(CompatibleArrayPoolListWriteBuffer) && readBufferType == typeof(CompatibleReadOnlySequenceReadBuffer)) { return CreateFormatter<CompatibleArrayPoolListWriteBuffer, CompatibleReadOnlySequenceReadBuffer>(valueType); }
            if (writeBufferType == typeof(CompatibleBufferWriterWriteBuffer) && readBufferType == typeof(CompatibleReadOnlySpanReadBuffer)) { return CreateFormatter<CompatibleBufferWriterWriteBuffer, CompatibleReadOnlySpanReadBuffer>(valueType); }
            if (writeBufferType == typeof(CompatibleBufferWriterWriteBuffer) && readBufferType == typeof(CompatibleReadOnlySequenceReadBuffer)) { return CreateFormatter<CompatibleBufferWriterWriteBuffer, CompatibleReadOnlySequenceReadBuffer>(valueType); }
            return null;
        }
#if NET9_0_OR_GREATER
        public override
#else
        public
#endif
        object? CreateFormatter<TWriteBuffer, TReadBuffer>(Type valueType)
#if !NET9_0_OR_GREATER
            where TWriteBuffer : struct, IWriteBuffer
            where TReadBuffer : struct, IReadBuffer
#endif
            => valueType == typeof(List<T>) ? new CborListFormatter<TWriteBuffer, TReadBuffer, T>() : null;
    }
}
/// <summary>Explicit collection provider. New factories create the buffer-specific formatter directly.</summary>
public sealed class CborNullableFormatter<T> : ICborFormatter<T?>, IFactoryFormatter where T : struct
{
    private readonly CborFormatterResolver resolver;
    private readonly Factory factory;
    CborFormatterResolver IFactoryFormatter.Resolver => resolver;
    /// <summary>Creates an explicit collection provider.</summary>
    public CborNullableFormatter() { factory = new Factory(); resolver = factory.CreateResolver(); }
    /// <inheritdoc />
    public void Serialize<TWriteBuffer>(ref TWriteBuffer buffer, ref CborSerializationContext context, T? value) where TWriteBuffer : struct, IWriteBuffer
    {
        var formatter = (ICborFormatter<TWriteBuffer, CompatibleReadOnlySpanReadBuffer, T?>)factory.CreateFormatter<TWriteBuffer, CompatibleReadOnlySpanReadBuffer>(typeof(T?))!;
        formatter.Initialize(context.Options.Resolver);
        context.SerializeSameItem(ref buffer, value, formatter);
    }
    /// <inheritdoc />
    public T? Deserialize<TReadBuffer>(ref TReadBuffer buffer, ref CborDeserializationContext context) where TReadBuffer : struct, IReadBuffer
    {
        var formatter = (ICborFormatter<CompatibleArrayPoolListWriteBuffer, TReadBuffer, T?>)factory.CreateFormatter<CompatibleArrayPoolListWriteBuffer, TReadBuffer>(typeof(T?))!;
        formatter.Initialize(context.Options.Resolver);
        return context.DeserializeSameItem(ref buffer, formatter);
    }
    private sealed class Factory : CborFormatterFactory
    {
        public override object? CreateFormatter(Type writeBufferType, Type readBufferType, Type valueType)
        {
            if (writeBufferType == typeof(CompatibleArrayPoolListWriteBuffer) && readBufferType == typeof(CompatibleReadOnlySpanReadBuffer)) { return CreateFormatter<CompatibleArrayPoolListWriteBuffer, CompatibleReadOnlySpanReadBuffer>(valueType); }
            if (writeBufferType == typeof(CompatibleArrayPoolListWriteBuffer) && readBufferType == typeof(CompatibleReadOnlySequenceReadBuffer)) { return CreateFormatter<CompatibleArrayPoolListWriteBuffer, CompatibleReadOnlySequenceReadBuffer>(valueType); }
            if (writeBufferType == typeof(CompatibleBufferWriterWriteBuffer) && readBufferType == typeof(CompatibleReadOnlySpanReadBuffer)) { return CreateFormatter<CompatibleBufferWriterWriteBuffer, CompatibleReadOnlySpanReadBuffer>(valueType); }
            if (writeBufferType == typeof(CompatibleBufferWriterWriteBuffer) && readBufferType == typeof(CompatibleReadOnlySequenceReadBuffer)) { return CreateFormatter<CompatibleBufferWriterWriteBuffer, CompatibleReadOnlySequenceReadBuffer>(valueType); }
            return null;
        }
#if NET9_0_OR_GREATER
        public override
#else
        public
#endif
        object? CreateFormatter<TWriteBuffer, TReadBuffer>(Type valueType)
#if !NET9_0_OR_GREATER
            where TWriteBuffer : struct, IWriteBuffer
            where TReadBuffer : struct, IReadBuffer
#endif
            => valueType == typeof(T?) ? new CborNullableFormatter<TWriteBuffer, TReadBuffer, T>() : null;
    }
}
/// <summary>Explicit collection provider. New factories create the buffer-specific formatter directly.</summary>
public sealed class CborDictionaryFormatter<TKey, TValue> : ICborFormatter<Dictionary<TKey, TValue>?>, IFactoryFormatter where TKey : notnull
{
    private readonly CborFormatterResolver resolver;
    private readonly Factory factory;
    CborFormatterResolver IFactoryFormatter.Resolver => resolver;
    /// <summary>Creates a provider with a process-keyed or explicit comparer.</summary>
    public CborDictionaryFormatter(IEqualityComparer<TKey>? comparer = null) { factory = new Factory(comparer ?? CborKeyComparers.Get<TKey>()); resolver = factory.CreateResolver(); }
    /// <inheritdoc />
    public void Serialize<TWriteBuffer>(ref TWriteBuffer buffer, ref CborSerializationContext context, Dictionary<TKey, TValue>? value) where TWriteBuffer : struct, IWriteBuffer
    {
        var formatter = (ICborFormatter<TWriteBuffer, CompatibleReadOnlySpanReadBuffer, Dictionary<TKey, TValue>?>)factory.CreateFormatter<TWriteBuffer, CompatibleReadOnlySpanReadBuffer>(typeof(Dictionary<TKey, TValue>))!;
        formatter.Initialize(context.Options.Resolver);
        context.SerializeSameItem(ref buffer, value, formatter);
    }
    /// <inheritdoc />
    public Dictionary<TKey, TValue>? Deserialize<TReadBuffer>(ref TReadBuffer buffer, ref CborDeserializationContext context) where TReadBuffer : struct, IReadBuffer
    {
        var formatter = (ICborFormatter<CompatibleArrayPoolListWriteBuffer, TReadBuffer, Dictionary<TKey, TValue>?>)factory.CreateFormatter<CompatibleArrayPoolListWriteBuffer, TReadBuffer>(typeof(Dictionary<TKey, TValue>))!;
        formatter.Initialize(context.Options.Resolver);
        return context.DeserializeSameItem(ref buffer, formatter);
    }
    private sealed class Factory(IEqualityComparer<TKey> comparer) : CborFormatterFactory
    {
        public override object? CreateFormatter(Type writeBufferType, Type readBufferType, Type valueType)
        {
            if (writeBufferType == typeof(CompatibleArrayPoolListWriteBuffer) && readBufferType == typeof(CompatibleReadOnlySpanReadBuffer)) { return CreateFormatter<CompatibleArrayPoolListWriteBuffer, CompatibleReadOnlySpanReadBuffer>(valueType); }
            if (writeBufferType == typeof(CompatibleArrayPoolListWriteBuffer) && readBufferType == typeof(CompatibleReadOnlySequenceReadBuffer)) { return CreateFormatter<CompatibleArrayPoolListWriteBuffer, CompatibleReadOnlySequenceReadBuffer>(valueType); }
            if (writeBufferType == typeof(CompatibleBufferWriterWriteBuffer) && readBufferType == typeof(CompatibleReadOnlySpanReadBuffer)) { return CreateFormatter<CompatibleBufferWriterWriteBuffer, CompatibleReadOnlySpanReadBuffer>(valueType); }
            if (writeBufferType == typeof(CompatibleBufferWriterWriteBuffer) && readBufferType == typeof(CompatibleReadOnlySequenceReadBuffer)) { return CreateFormatter<CompatibleBufferWriterWriteBuffer, CompatibleReadOnlySequenceReadBuffer>(valueType); }
            return null;
        }
#if NET9_0_OR_GREATER
        public override
#else
        public
#endif
        object? CreateFormatter<TWriteBuffer, TReadBuffer>(Type valueType)
#if !NET9_0_OR_GREATER
            where TWriteBuffer : struct, IWriteBuffer
            where TReadBuffer : struct, IReadBuffer
#endif
            => valueType == typeof(Dictionary<TKey, TValue>) ? new CborDictionaryFormatter<TWriteBuffer, TReadBuffer, TKey, TValue>(comparer) : null;
    }
}
