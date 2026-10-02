#if VERIFY_OWNERSHIP_VIOLATION
using Cbor;

internal static class ContextOwnershipViolation
{
    public static void CopyContext()
    {
        var context = new CborSerializationContext(CborSerializerOptions.Default);
        var copy = context; // The installed analyzer must reject ownership duplication with CBOR003.
        copy.Dispose();
        context.Dispose();
    }
}
#endif
