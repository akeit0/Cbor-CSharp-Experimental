using Cbor.Internal;

namespace Cbor;

/// <summary>A decoded CBOR header, without interpreting or consuming its payload or children.</summary>
/// <remarks>A successful header decode does not establish that the enclosing item is valid.
/// Floating-point arguments contain their raw bits. An indefinite header or break has argument zero.</remarks>
public readonly struct CborHeader
{
    internal CborHeader(CborMajorType majorType, byte additionalInformation, ulong argument, int encodedLength)
    {
        MajorType = majorType;
        AdditionalInformation = additionalInformation;
        Argument = argument;
        EncodedLength = encodedLength;
    }

    /// <summary>The token's major type.</summary>
    public CborMajorType MajorType { get; }

    /// <summary>The initial byte's low five bits, including its original width indicator.</summary>
    public byte AdditionalInformation { get; }

    /// <summary>Integer argument, payload length, child count, tag number, simple value, or raw float bits.</summary>
    public ulong Argument { get; }

    /// <summary>The header size in bytes, including float or simple-value argument bytes.</summary>
    public int EncodedLength { get; }

    /// <summary>Whether this begins an indefinite-length string, array, or map.</summary>
    public bool IsIndefiniteLength => AdditionalInformation == CborEncoding.IndefiniteArgument && MajorType != CborMajorType.Simple;

    /// <summary>Whether this is a break marker. Only an enclosing indefinite item can make it legal.</summary>
    public bool IsBreak => MajorType == CborMajorType.Simple && AdditionalInformation == CborEncoding.IndefiniteArgument;
}
