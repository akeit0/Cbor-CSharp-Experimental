using System.Buffers.Binary;
using Cbor.Internal;

namespace Cbor.Tests;

public sealed class KeyComparerTests
{
    [Fact]
    public void SipHashMatchesAll64AuthorKnownAnswerVectors()
    {
        using var stream = typeof(KeyComparerTests).Assembly.GetManifestResourceStream("Cbor.Fixtures.siphash24.txt")!;
        using var reader = new StreamReader(stream);
        byte[] source = Enumerable.Range(0, 64).Select(static i => (byte)i).ToArray();
        int count = 0;
        while (reader.ReadLine() is { } line)
        {
            ulong expected = BinaryPrimitives.ReadUInt64LittleEndian(Convert.FromHexString(line));
            Assert.Equal(expected, SipHash24.Hash(source.AsSpan(0, count), 0x0706050403020100, 0x0f0e0d0c0b0a0908));
            count++;
        }

        Assert.Equal(64, count);
    }

    [Fact]
    public void FloatingPointComparerPreservesClrZeroAndNanEquality()
    {
        var doubles = CborKeyComparers.Get<double>();
        Assert.Equal(doubles.GetHashCode(0), doubles.GetHashCode(-0.0));
        Assert.Equal(doubles.GetHashCode(double.NaN), doubles.GetHashCode(BitConverter.Int64BitsToDouble(0x7ff0000000000001)));
        var singles = CborKeyComparers.Get<float>();
        Assert.Equal(singles.GetHashCode(0), singles.GetHashCode(-0.0f));
        Assert.Equal(singles.GetHashCode(float.NaN), singles.GetHashCode(BitConverter.Int32BitsToSingle(0x7f800001)));
        var strings = CborKeyComparers.Get<string>();
        Assert.True(strings.Equals("水😀", new string("水😀".ToCharArray())));
        Assert.Equal(strings.GetHashCode("水😀"), strings.GetHashCode(new string("水😀".ToCharArray())));
        Assert.False(strings.Equals("A", "a"));
    }

    [Fact]
    public void PredictableInt64DefaultHashCollisionsDoNotCarryOver()
    {
        var comparer = CborKeyComparers.Get<long>();
        var hashes = new HashSet<int>();
        for (long value = 0; value < 10_000; value++)
        {
            long key = (value << 32) | value;
            Assert.Equal(0, key.GetHashCode());
            hashes.Add(comparer.GetHashCode(key));
        }

        Assert.True(hashes.Count > 9_900);
    }

    [Fact]
    public void CustomKeyTypesRequireAnExplicitComparer()
    {
        Assert.Throws<NotSupportedException>(() => new CborDictionaryFormatter<Point, int>());
        Assert.NotNull(new CborDictionaryFormatter<Point, int>(EqualityComparer<Point>.Default));
    }
}
