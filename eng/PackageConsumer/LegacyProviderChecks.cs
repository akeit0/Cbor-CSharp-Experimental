using System.Buffers;
using System.Runtime.Versioning;
using Cbor;
using LegacyPackageProvider;

internal static class LegacyProviderChecks
{
    public static void Run()
    {
        var target = (TargetFrameworkAttribute?)Attribute.GetCustomAttribute(typeof(LegacyResolver).Assembly, typeof(TargetFrameworkAttribute));
        if (target?.FrameworkName != ".NETStandard,Version=v2.0") { throw new InvalidOperationException("The downlevel provider test selected a modern asset."); }
        var scalarOptions = new CborSerializerOptions(CborFormatterFactory.Combine(new LegacyIntegerFactory(), CborFormatterFactory.Builtin).CreateResolver());
        for (int i = 0; i < 10; i++)
        {
            byte[] scalarBytes = CborSerializer.Serialize(1, scalarOptions);
            if (scalarBytes.Length != 1 || scalarBytes[0] != 6 || CborSerializer.Deserialize<int>(scalarBytes, scalarOptions) != 1)
            { throw new InvalidOperationException("A downlevel paired factory lost precedence over modern built-ins."); }
        }
        var overrideResolver = new CborFormatterRegistry().Add(new LegacyOffsetFormatter()).Build();
        var factory = CborFormatterFactory.Combine(CborFormatterFactory.FromResolver(LegacyResolver.Instance), CborFormatterFactory.Builtin);
        var options = new CborSerializerOptions(new CborCompositeResolver(overrideResolver, factory.CreateResolver()));
        var value = new LegacyNode { Value = 1, Child = new LegacyNode { Value = 2 } };
        byte[] bytes = CborSerializer.Serialize(value, options);
        if (!bytes.AsSpan().SequenceEqual(Convert.FromHexString("A2000201A2000301F6"))) { throw new InvalidOperationException("A downlevel graph lost the caller's numeric override."); }
        for (int i = 0; i < 10; i++)
        {
            var restored = CborSerializer.Deserialize<LegacyNode>(bytes, options);
            if (restored.Value != 1 || restored.Child?.Value != 2) { throw new InvalidOperationException("The downlevel recursive graph failed."); }
            var writer = new ArrayBufferWriter<byte>();
            CborSerializer.Serialize(writer, value, options);
            if (!writer.WrittenSpan.SequenceEqual(bytes)) { throw new InvalidOperationException("The downlevel writer pair failed."); }
            var source = new ReadOnlySequence<byte>(bytes);
            if (CborSerializer.Deserialize<LegacyNode>(in source, options).Child?.Value != 2) { throw new InvalidOperationException("The downlevel sequence pair failed."); }
        }
        Console.WriteLine("Downlevel generated provider and legacy formatter passed.");
    }
}
