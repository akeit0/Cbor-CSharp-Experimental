#if VERIFY_OWNERSHIP_VIOLATION
using Cbor;

internal static class ContextCollectionOwnershipViolation
{
    public static void CopyIntoCollection()
    {
        var context = new CborSerializationContext(CborSerializerOptions.Default);
        CborSerializationContext[] copies = [context]; // Exercise newer host syntax with the Roslyn 4.3 analyzer binary.
        context.Dispose();
    }
}
#endif
