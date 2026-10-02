namespace Cbor.Internal;

internal static class CborError
{
    internal static void Throw(CborDecodeResult result)
    {
        switch (result)
        {
            case CborDecodeResult.NeedMoreData:
                throw new EndOfStreamException("The CBOR item is truncated.");
            case CborDecodeResult.TypeMismatch:
                throw new InvalidDataException("The CBOR token has a different type than requested.");
            case CborDecodeResult.Overflow:
                throw new OverflowException("The CBOR value does not fit the requested CLR type.");
            case CborDecodeResult.LimitExceeded:
                throw new InvalidDataException("The CBOR item exceeds the configured resource limits.");
            case CborDecodeResult.EncodingPolicyViolation:
                throw new InvalidDataException("The CBOR encoding is forbidden by the configured policy.");
            default:
                throw new InvalidDataException("The CBOR item is malformed.");
        }
    }
}
