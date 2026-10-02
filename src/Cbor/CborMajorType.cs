namespace Cbor;

/// <summary>The three-bit major type in a CBOR initial byte.</summary>
public enum CborMajorType : byte
{
    /// <summary>Nonnegative integers.</summary>
    UnsignedInteger,
    /// <summary>Negative integers, encoded as the argument of -1 minus argument.</summary>
    NegativeInteger,
    /// <summary>Byte strings.</summary>
    ByteString,
    /// <summary>UTF-8 text strings.</summary>
    TextString,
    /// <summary>Arrays of data items.</summary>
    Array,
    /// <summary>Maps of key/value pairs.</summary>
    Map,
    /// <summary>Semantic tags applied to one following data item.</summary>
    Tag,
    /// <summary>Simple values, floating-point numbers, or a contextual break marker.</summary>
    Simple,
}
