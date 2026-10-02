using System.Text;
using Cbor.Internal;
using SerializerFoundation;

namespace Cbor.Tests;

public sealed class RuntimeImprovementTests
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    [Fact]
    public void EveryUnicodeScalarSurvivesBulkValidationAndFragmentation()
    {
        var text = new StringBuilder();
        for (int scalar = 0; scalar <= 0x10ffff; scalar++)
        {
            if (scalar is >= 0xd800 and <= 0xdfff)
            {
                continue;
            }

            if (scalar <= char.MaxValue)
            {
                text.Append((char)scalar);
            }
            else
            {
                int adjusted = scalar - 0x10000;
                text.Append((char)(0xd800 + (adjusted >> 10)));
                text.Append((char)(0xdc00 + (adjusted & 0x3ff)));
            }
        }

        byte[] encoded = StrictUtf8.GetBytes(text.ToString());
        Assert.True(Utf8Validator.IsValid(encoded));
        int[] chunkSizes = [1, 2, 3, 4, 7, 4093];
        foreach (int size in chunkSizes)
        {
            var validator = new Utf8Validator();
            for (int offset = 0; offset < encoded.Length; offset += size)
            {
                Assert.True(validator.Append(encoded.AsSpan(offset, Math.Min(size, encoded.Length - offset))));
            }

            Assert.True(validator.IsComplete);
        }
    }

    [Fact]
    public void Utf8BulkLeadAndContinuationBoundariesMatchTheRuntimeOracle()
    {
        byte[] continuations = [0x00, 0x7f, 0x80, 0x8f, 0x90, 0x9f, 0xa0, 0xbf, 0xc0, 0xff];
        Span<byte> encoded = stackalloc byte[4];
        for (int lead = 0; lead <= byte.MaxValue; lead++)
        {
            encoded[0] = (byte)lead;
            for (int second = 0; second <= byte.MaxValue; second++)
            {
                encoded[1] = (byte)second;
                foreach (byte third in continuations)
                {
                    encoded[2] = third;
                    foreach (byte fourth in continuations)
                    {
                        encoded[3] = fourth;
                        Assert.Equal(System.Text.Unicode.Utf8.IsValid(encoded), Utf8Validator.IsValid(encoded));
                    }
                }
            }
        }
    }

    [Fact]
    public void GeneratedRepeatedTypesResolveOnceAndRespectEachOperationsResolver()
    {
        var resolver = new CountingResolver(TestResolver.Instance);
        var options = new CborSerializerOptions(resolver);
        var value = new Point { X = 1, Y = 2 };
        byte[] bytes = CborSerializer.Serialize(value, options);
        Assert.Equal(1, resolver.Count<int>());
        Assert.Equal("A200010102", Convert.ToHexString(bytes));
        Assert.Equal(2, CborSerializer.Deserialize<Point>(bytes, options).Y);
        Assert.Equal(2, resolver.Count<int>());

        var overrides = new CborFormatterRegistry().Add(new OffsetFormatter()).Build();
        var alternate = new CborSerializerOptions(new CborCompositeResolver(overrides, TestResolver.Instance));
        Assert.Equal("A200020103", Convert.ToHexString(CborSerializer.Serialize(value, alternate)));
        Assert.Equal(2, CborSerializer.Deserialize<Point>(Convert.FromHexString("A200020103"), alternate).Y);
        Assert.Equal(bytes, CborSerializer.Serialize(value, options));
    }

    [Fact]
    public void MissingOptionalMembersAndNullObjectsDoNotResolveUnusedFormatters()
    {
        var resolver = new CountingResolver(TestResolver.Instance);
        var options = new CborSerializerOptions(resolver);
        Assert.Null(CborSerializer.Deserialize<Person>([0xf6], options));
        Assert.Equal(0, resolver.Count<int>());
        Assert.Equal(1, CborSerializer.Deserialize<Person>(Convert.FromHexString("A10001"), options).Id);
        Assert.Equal(1, resolver.Count<int>());
        Assert.Equal(0, resolver.Count<string>());
        var person = CborSerializer.Deserialize<Person>(CborSerializer.Serialize(new Person { Id = 3, Name = null }, options), options);
        Assert.Null(person.Name);
    }

    [Theory]
    [InlineData("A3000101020203")]
    [InlineData("BF000101020203FF")]
    public void DictionaryInsertionUsesComparerAndOneModernHashPerKey(string hex)
    {
        var comparer = new CountingComparer();
        var options = new CborSerializerOptions(new CborFormatterRegistry()
            .Add(new CborDictionaryFormatter<int, int>(comparer)).Build());
        var result = CborSerializer.Deserialize<Dictionary<int, int>>(Convert.FromHexString(hex), options);
        Assert.Equal(3, result.Count);
#if NET8_0_OR_GREATER && !CBOR_NETSTANDARD20 && !CBOR_NETSTANDARD21
        Assert.Equal(3, comparer.HashCalls);
#else
        // A definite map preallocates buckets, so ContainsKey hashes even the first key.
        Assert.Equal(hex[0] == 'A' ? 6 : 5, comparer.HashCalls);
#endif
        Assert.Same(comparer, result.Comparer);
        Assert.Equal(3, result[2]);
    }

    [Theory]
    [InlineData("A2616101614102")]
    [InlineData("BF616101614102FF")]
    public void DuplicateDictionaryKeyFailsBeforeCallingItsValueFormatter(string hex)
    {
        var formatter = new OffsetFormatter();
        var options = new CborSerializerOptions(new CborFormatterRegistry()
            .Add(new CborDictionaryFormatter<string, int>(StringComparer.OrdinalIgnoreCase))
            .Add(formatter).Build());
        // Two differently encoded keys that the supplied comparer considers equal.
        Assert.Throws<InvalidDataException>(() => CborSerializer.Deserialize<Dictionary<string, int>>(Convert.FromHexString(hex), options));
        Assert.Equal(1, formatter.Reads);
    }

    [Fact]
    public void DictionaryValueFailureAndComparerExceptionsPropagate()
    {
        var failure = new InvalidOperationException("comparer failure");
        var options = new CborSerializerOptions(new CborFormatterRegistry()
            .Add(new CborDictionaryFormatter<int, int>(new ThrowingComparer(failure))).Build());
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() =>
            CborSerializer.Deserialize<Dictionary<int, int>>(Convert.FromHexString("A10001"), options)));
        var ordinary = new CborSerializerOptions(new CborFormatterRegistry().Add(new CborDictionaryFormatter<int, int>()).Build());
        Assert.Throws<InvalidDataException>(() => CborSerializer.Deserialize<Dictionary<int, int>>(Convert.FromHexString("A100F6"), ordinary));
        Assert.Equal(1, CborSerializer.Deserialize<Dictionary<int, int>>(Convert.FromHexString("A10001"), ordinary)[0]);
    }

    [Fact]
    public void Utf8StreamingValidationMatchesStrictEncodingAcrossEveryBoundary()
    {
        string[] points = ["a", "\u007f", "\u0080", "\u07ff", "\u0800", "\ud7ff", "\ue000", "\uffff", "𐀀", "\U0010ffff"];
        foreach (string point in points)
        {
            for (int padding = 0; padding < 16; padding++)
            {
                CheckEverySeam(Encoding.UTF8.GetBytes(new string('a', 64 + padding) + point + new string('b', padding)));
            }
        }

        string[] malformed = ["80", "C080", "EDA080", "F4908080", "F0808080", "E282", "FFFFFFFF", "8080808080808080808080808080808080"];
        foreach (string hex in malformed)
        {
            for (int padding = 0; padding < 8; padding++)
            {
                byte[] prefix = Encoding.UTF8.GetBytes(new string('a', 64 + padding));
                CheckEverySeam(prefix.Concat(Convert.FromHexString(hex)).Concat(new byte[padding]).ToArray());
            }
        }

        var random = new Random(934_012);
        for (int i = 0; i < 1000; i++)
        {
            byte[] bytes = new byte[random.Next(96)];
            random.NextBytes(bytes);
            CheckEverySeam(bytes);
        }
    }

    private static void CheckEverySeam(byte[] bytes)
    {
        bool expected;
        try { StrictUtf8.GetCharCount(bytes); expected = true; }
        catch (DecoderFallbackException) { expected = false; }
        Assert.Equal(expected, Utf8Validator.IsValid(bytes));
        for (int seam = 0; seam <= bytes.Length; seam++)
        {
            var validator = new Utf8Validator();
            bool actual = validator.Append(bytes.AsSpan(0, seam)) && validator.Append([]) &&
                validator.Append(bytes.AsSpan(seam)) && validator.IsComplete;
            Assert.Equal(expected, actual);
        }

        var fragmented = new Utf8Validator();
        bool valid = true;
        foreach (byte value in bytes)
        {
            Span<byte> single = [value];
            valid &= fragmented.Append(single);
        }

        Assert.Equal(expected, valid && fragmented.IsComplete);
    }

    private sealed class CountingResolver(CborFormatterResolver inner) : CborFormatterResolver
    {
        private readonly Dictionary<Type, int> counts = [];
        public override ICborFormatter<T>? GetFormatter<T>()
        {
            counts.TryGetValue(typeof(T), out int count);
            counts[typeof(T)] = count + 1;
            return inner.GetFormatter<T>();
        }

        internal int Count<T>() => counts.TryGetValue(typeof(T), out int count) ? count : 0;
    }

    private sealed class CountingComparer : IEqualityComparer<int>
    {
        internal int HashCalls { get; private set; }
        public bool Equals(int x, int y) => x == y;
        public int GetHashCode(int obj) { HashCalls++; return 0; } // Exercise collision chains too.
    }

    private sealed class ThrowingComparer(Exception failure) : IEqualityComparer<int>
    {
        public bool Equals(int x, int y) => x == y;
        public int GetHashCode(int obj) => throw failure;
    }

    private sealed class OffsetFormatter : ICborFormatter<int>
    {
        internal int Reads { get; private set; }
        public void Serialize<W>(ref W buffer, ref CborSerializationContext context, int value)
            where W : struct, IWriteBuffer
#if NET9_0_OR_GREATER
                , allows ref struct
#endif
            => buffer.WriteInt64(value + 1);

        public int Deserialize<R>(ref R buffer, ref CborDeserializationContext context)
            where R : struct, IReadBuffer
#if NET9_0_OR_GREATER
                , allows ref struct
#endif
        { Reads++; return checked((int)buffer.ReadInt64() - 1); }
    }
}
