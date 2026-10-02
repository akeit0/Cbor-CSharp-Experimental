using System.Buffers;
using Cbor.Internal;
using SerializerFoundation;

namespace Cbor;

/// <summary>Bounded, iterative CBOR syntax and UTF-8 validation for borrowed spans and sequences.</summary>
/// <remarks>Validation does not interpret registered tag contents or establish map-key uniqueness.
/// Use a protocol's semantic rules in addition to these checks. Failed prefix reads return zero
/// length; the caller's memory/sequence is never advanced or modified.</remarks>
public static class CborValidation
{
    /// <summary>Validates exactly one item, rejecting trailing bytes.</summary>
    public static CborDecodeResult TryValidate(ReadOnlySpan<byte> source, CborReaderOptions? options = null)
    {
        var result = TryReadValueLength(source, out int length, options);
        return result == CborDecodeResult.Success && length != source.Length ? CborDecodeResult.InvalidData : result;
    }

    /// <summary>Validates exactly one item in a segmented source, rejecting trailing bytes.</summary>
    public static CborDecodeResult TryValidate(in ReadOnlySequence<byte> source, CborReaderOptions? options = null)
    {
        var result = TryReadValueLength(in source, out long length, options);
        return result == CborDecodeResult.Success && length != source.Length ? CborDecodeResult.InvalidData : result;
    }

    /// <summary>Finds and validates the first complete item, allowing following items to remain in the source.</summary>
    public static unsafe CborDecodeResult TryReadValueLength(ReadOnlySpan<byte> source, out int encodedLength, CborReaderOptions? options = null)
    {
        encodedLength = 0;
#if NET9_0_OR_GREATER
        var buffer = new ReadOnlySpanReadBuffer(source);
        try
        {
            var result = CborScanner.Scan(ref buffer, options ?? CborReaderOptions.Default);
            if (result == CborDecodeResult.Success)
            {
                encodedLength = (int)buffer.BytesConsumed;
            }

            return result;
        }
        finally
        {
            buffer.Dispose();
        }
#else
        // Compatibility tier keeps the borrowed memory pinned for the complete buffer lifetime.
        fixed (byte* pointer = source)
        {
            var buffer = new CompatibleReadOnlySpanReadBuffer(pointer, source.Length);
            try
            {
                var result = CborScanner.Scan(ref buffer, options ?? CborReaderOptions.Default);
                if (result == CborDecodeResult.Success)
                {
                    encodedLength = (int)buffer.BytesConsumed;
                }

                return result;
            }
            finally
            {
                buffer.Dispose();
            }
        }
#endif
    }

    /// <summary>Finds and validates the first complete item without flattening a segmented source.</summary>
    public static CborDecodeResult TryReadValueLength(in ReadOnlySequence<byte> source, out long encodedLength, CborReaderOptions? options = null)
    {
        encodedLength = 0;
#if NET9_0_OR_GREATER
        Span<byte> scratch = stackalloc byte[CborPrimitives.MaxHeaderLength];
        var buffer = new ReadOnlySequenceReadBuffer(in source, scratch);
#else
        var buffer = new CompatibleReadOnlySequenceReadBuffer(in source);
#endif
        try
        {
            var result = CborScanner.Scan(ref buffer, options ?? CborReaderOptions.Default);
            if (result == CborDecodeResult.Success)
            {
                encodedLength = buffer.BytesConsumed;
            }

            return result;
        }
        finally
        {
            buffer.Dispose();
        }
    }
}
