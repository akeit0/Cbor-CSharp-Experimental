using System.Runtime.Versioning;
using Cbor;

namespace Cbor.Benchmarks;

internal static class RuntimeAssetCheck
{
    internal static void Verify()
    {
        var attribute = (TargetFrameworkAttribute?)Attribute.GetCustomAttribute(typeof(CborPrimitives).Assembly, typeof(TargetFrameworkAttribute));
#if CBOR_NETSTANDARD20
        const string expected = ".NETStandard,Version=v2.0";
#elif NET10_0_OR_GREATER
        const string expected = ".NETCoreApp,Version=v10.0";
#elif NET9_0_OR_GREATER
        const string expected = ".NETCoreApp,Version=v9.0";
#else
        const string expected = ".NETCoreApp,Version=v8.0";
#endif
        if (attribute?.FrameworkName != expected)
        {
            throw new InvalidOperationException($"Expected CBOR asset {expected}, loaded {attribute?.FrameworkName} from {typeof(CborPrimitives).Assembly.Location}.");
        }
        Console.WriteLine($"Verified CBOR asset {expected}.");
    }
}
