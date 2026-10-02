namespace Cbor.Internal;

// RFC 8949 initial-byte layout and encoded token sizes.
internal static class CborEncoding
{
    internal const int BitsPerByte = 8;
    internal const int UInt64BitCount = 64;
    internal const int MajorTypeShift = 5;
    internal const byte AdditionalInformationMask = 0x1f;
    internal const byte NegativeIntegerPrefix = 0x20;
    internal const byte ByteStringPrefix = 0x40;
    internal const byte TagPrefix = 0xc0;
    internal const byte SimplePrefix = 0xe0;

    internal const byte DirectArgumentLimit = 24;
    internal const byte UInt8Argument = 24;
    internal const byte UInt16Argument = 25;
    internal const byte UInt32Argument = 26;
    internal const byte UInt64Argument = 27;
    internal const byte FirstReservedArgument = 28;
    internal const byte LastReservedArgument = 30;
    internal const byte IndefiniteArgument = 31;
    internal const byte MinimumExtendedSimpleValue = 32;
    internal const byte Float16Argument = 25;
    internal const byte Float32Argument = 26;
    internal const byte Float64Argument = 27;

    internal const int ImmediateHeaderLength = 1;
    internal const int UInt8HeaderLength = 2;
    internal const int UInt16HeaderLength = 3;
    internal const int UInt32HeaderLength = 5;
    internal const int UInt64HeaderLength = 9;
    internal const int ArgumentOffset = 1;

    internal const byte False = 0xf4;
    internal const byte True = 0xf5;
    internal const byte Null = 0xf6;
    internal const byte Undefined = 0xf7;
    internal const byte FalseValue = False & AdditionalInformationMask;
    internal const byte TrueValue = True & AdditionalInformationMask;
    internal const byte NullValue = Null & AdditionalInformationMask;
    internal const byte UndefinedValue = Undefined & AdditionalInformationMask;
    internal const byte ExtendedSimple = 0xf8;
    internal const byte Float16 = 0xf9;
    internal const byte Float32 = 0xfa;
    internal const byte Float64 = 0xfb;
    internal const byte Break = 0xff;
    internal const byte IndefiniteByteString = 0x5f;
    internal const byte IndefiniteTextString = 0x7f;
    internal const int Float16Length = 3;
    internal const int Float32Length = 5;
    internal const int Float64Length = 9;
    internal const ushort CanonicalHalfNaN = 0x7e00;
}
