#if VERIFY_OWNERSHIP_VIOLATION
using SerializerFoundation;

internal static class BufferOwnershipViolation
{
    internal static void CopyOwnedBuffer()
    {
        var original = new CompatibleArrayPoolListWriteBuffer();
        try
        {
            // Intentionally incorrect: the installed Foundation analyzer must reject this copy as SF002.
            var copy = original;
            copy.Dispose();
        }
        finally
        {
            original.Dispose();
        }
    }
}
#endif
