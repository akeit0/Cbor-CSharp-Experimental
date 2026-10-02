extern alias PeterOCbor;

using System.Reflection;
using System.Runtime.Versioning;
using MessagePack;
using SerializerFoundation;
using CBORObject = PeterOCbor::PeterO.Cbor.CBORObject;

namespace Cbor.Benchmarks.Comparison;

internal static class Codecs
{
    internal static readonly CborSerializerOptions CborOptions = new(ComparisonFactory.Instance);
    internal static readonly MessagePackSerializerOptions MessagePackOptions = new(
        new MessagePackFormatterResolver(MessagePackFormatterFactory.DefaultAot, throwOnLegacyFormatter: true))
    { MaxDepth = 64 };

    internal static byte[] CborEncode<T>(T value) => CborSerializer.Serialize(value, CborOptions);
    internal static T CborDecode<T>(byte[] data) => CborSerializer.Deserialize<T>(data, CborOptions);
    internal static byte[] MessagePackEncode<T>(T value) => MessagePackSerializer.Serialize(value, MessagePackOptions);
    internal static T MessagePackDecode<T>(byte[] data) => MessagePackSerializer.Deserialize<T>(data, MessagePackOptions);
    internal static byte[] RedoxEncode<T>(T value) => REDox.Cbor.CborSerializer.Serialize(value);
    internal static T RedoxDecode<T>(byte[] data) => REDox.Cbor.CborSerializer.Deserialize<T>(data)!;
    internal static byte[] PeterEncode<T>(T value) => CBORObject.FromObject(value).EncodeToBytes();
    internal static T PeterDecode<T>(byte[] data) => CBORObject.DecodeFromBytes(data).ToObject<T>();

    internal static void VerifyAssets()
    {
        if (Environment.Version.Major != 10) { throw new InvalidOperationException("Comparison workers must execute on .NET 10."); }
        VerifyTarget(typeof(CborSerializer).Assembly, ".NETCoreApp,Version=v10.0");
        VerifyTarget(typeof(MessagePackSerializer).Assembly, ".NETCoreApp,Version=v10.0");
        VerifyTarget(typeof(REDox.Cbor.CborSerializer).Assembly, ".NETCoreApp,Version=v10.0");
        VerifyTarget(typeof(CBORObject).Assembly, ".NETStandard,Version=v1.0");
        VerifyTarget(typeof(System.Formats.Cbor.CborReader).Assembly, ".NETCoreApp,Version=v10.0");
        var formatter = MessagePackOptions.Resolver.GetFormatter<ArrayPoolListWriteBuffer, ReadOnlySpanReadBuffer, OrderBatch>();
        if (formatter.GetType().Assembly != typeof(OrderBatch).Assembly)
        {
            throw new InvalidOperationException("MessagePack models must use the comparison assembly's generated formatter.");
        }
    }

    private static void VerifyTarget(Assembly assembly, string expected)
    {
        string? actual = assembly.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName;
        if (actual != expected)
        {
            throw new InvalidOperationException($"Incorrect asset for {assembly.GetName().Name}: {actual}; expected {expected}.");
        }
        Console.WriteLine($"Asset: {assembly.GetName().Name} {assembly.GetName().Version} {actual}");
    }
}
