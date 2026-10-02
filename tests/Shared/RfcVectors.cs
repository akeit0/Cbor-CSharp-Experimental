namespace Cbor.Testing;

internal static class RfcVectors
{
    internal static string[] Valid { get; } = Load("rfc8949-appendix-a.txt");
    internal static string[] Invalid { get; } = Load("rfc8949-appendix-f-invalid.txt");

    private static string[] Load(string name)
    {
        using var stream = typeof(RfcVectors).Assembly.GetManifestResourceStream("Cbor.Fixtures." + name)
            ?? throw new InvalidOperationException("Missing embedded fixture: " + name);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
    }
}
