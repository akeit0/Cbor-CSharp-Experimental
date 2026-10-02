using SerializerFoundation;
namespace Cbor.Internal;

internal sealed class BuiltinFormatterFactory : CborFormatterFactory
{
    public override object? CreateFormatter<TWriteBuffer, TReadBuffer>(Type valueType)
    {
        if (valueType == typeof(CborInteger)) { return new IntegerFormatter<TWriteBuffer, TReadBuffer>(); }
        if (valueType == typeof(CborSimpleValue)) { return new SimpleValueFormatter<TWriteBuffer, TReadBuffer>(); }
        if (valueType == typeof(System.Numerics.BigInteger)) { return new BigIntegerFormatter<TWriteBuffer, TReadBuffer>(); }
#if NET8_0_OR_GREATER
        if (valueType == typeof(Half)) { return new CborHalfFormatter<TWriteBuffer, TReadBuffer>(); }
#endif
        if (valueType == typeof(bool)) { return new BooleanFormatter<TWriteBuffer, TReadBuffer>(); }
        if (valueType == typeof(byte)) { return new ByteFormatter<TWriteBuffer, TReadBuffer>(); }
        if (valueType == typeof(sbyte)) { return new SByteFormatter<TWriteBuffer, TReadBuffer>(); }
        if (valueType == typeof(short)) { return new Int16Formatter<TWriteBuffer, TReadBuffer>(); }
        if (valueType == typeof(ushort)) { return new UInt16Formatter<TWriteBuffer, TReadBuffer>(); }
        if (valueType == typeof(int)) { return new Int32Formatter<TWriteBuffer, TReadBuffer>(); }
        if (valueType == typeof(uint)) { return new UInt32Formatter<TWriteBuffer, TReadBuffer>(); }
        if (valueType == typeof(long)) { return new Int64Formatter<TWriteBuffer, TReadBuffer>(); }
        if (valueType == typeof(ulong)) { return new UInt64Formatter<TWriteBuffer, TReadBuffer>(); }
        if (valueType == typeof(double)) { return new DoubleFormatter<TWriteBuffer, TReadBuffer>(); }
        if (valueType == typeof(float)) { return new SingleFormatter<TWriteBuffer, TReadBuffer>(); }
        if (valueType == typeof(string)) { return new StringFormatter<TWriteBuffer, TReadBuffer>(); }
        if (valueType == typeof(byte[])) { return new ByteStringFormatter<TWriteBuffer, TReadBuffer>(); }
        return null;
    }
}
