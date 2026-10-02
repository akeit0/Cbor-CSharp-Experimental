namespace Cbor;

/// <summary>Outcome of a non-throwing CBOR decode or validation operation.</summary>
public enum CborDecodeResult
{
    /// <summary>The requested token or item was decoded.</summary>
    Success,
    /// <summary>The input ends before the token or item is complete.</summary>
    NeedMoreData,
    /// <summary>The input violates CBOR syntax or UTF-8 validity.</summary>
    InvalidData,
    /// <summary>A well-formed token has a different type than requested.</summary>
    TypeMismatch,
    /// <summary>The value cannot be represented by the requested CLR type.</summary>
    Overflow,
    /// <summary>The input exceeds a configured byte, item, or depth limit.</summary>
    LimitExceeded,
    /// <summary>A header uses an encoding forbidden by the configured policy.</summary>
    EncodingPolicyViolation,
}
