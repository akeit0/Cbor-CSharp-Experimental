using System.Text;

namespace Cbor.Internal;

internal static class CborTextEncoding
{
    internal static readonly UTF8Encoding StrictUtf8 = new(false, true);
}
