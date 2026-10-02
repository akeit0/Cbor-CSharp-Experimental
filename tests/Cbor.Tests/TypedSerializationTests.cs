using System.Buffers;
using Cbor.Testing;
using SerializerFoundation;

namespace Cbor.Tests;

public sealed class TypedSerializationTests
{
    private static readonly CborSerializerOptions Options = new(TestResolver.Instance);

    [Fact]
    public void NestedPayloadCannotRequestMemoryBeyondTheRemainingEncodedBudget()
    {
        string text = new('a', 1024);
        string[] values = [text, text];
        var options = new CborSerializerOptions(
            new CborFormatterRegistry().Add(new CborArrayFormatter<string>()).Build(),
            new CborReaderOptions(maxEncodedLength: 1536));
        var output = new ArrayBufferWriter<byte>();
        var buffer = new CompatibleBufferWriterWriteBuffer(output);
        try
        {
            try
            {
                CborSerializer.Serialize(ref buffer, values, options);
                Assert.Fail("Oversized item accepted.");
            }
            catch (InvalidDataException)
            {
                Assert.True(buffer.BytesWritten <= 1536, $"Published {buffer.BytesWritten} bytes before rejecting the payload.");
            }
        }
        finally { buffer.Dispose(); }
    }

    [Fact]
    public void CollectionLookupHoistingPreservesPerOperationOverrides()
    {
        var options = new CborSerializerOptions(new CborCompositeResolver(
            new CborFormatterRegistry().Add(new PlusOneFormatter()).Build(), TestResolver.Instance));
        int[] values = [1, 2, 3];
        Assert.Equal("83020304", Convert.ToHexString(CborSerializer.Serialize(values, options)));
        Assert.Equal([1, 2, 3], CborSerializer.Deserialize<int[]>(Convert.FromHexString("83020304"), options));
        Assert.Equal([1, 2, 3], CborSerializer.Deserialize<List<int>>(Convert.FromHexString("9F020304FF"), options));
        Assert.Equal("83010203", Convert.ToHexString(CborSerializer.Serialize(values, Options)));
    }

    [Theory]
    [InlineData("1801")]
    [InlineData("190018")]
    [InlineData("1A00000100")]
    [InlineData("1B0000000000010000")]
    public void ExtendedIntegerColdPathEnforcesPreferredEncoding(string hex)
    {
        byte[] input = Convert.FromHexString(hex);
        Assert.Throws<InvalidDataException>(() => CborSerializer.Deserialize<ulong>(input,
            new CborSerializerOptions(readerOptions: new CborReaderOptions(requirePreferredEncoding: true))));
        CborSerializer.Deserialize<ulong>(input);
    }

    [Fact]
    public void IntegerFormatterOverridesCannotChangeObjectWireKeys()
    {
        var overrides = new CborFormatterRegistry().Add(new PlusOneFormatter()).Build();
        var options = new CborSerializerOptions(new CborCompositeResolver(overrides, TestResolver.Instance));
        byte[] encoded = CborSerializer.Serialize(new Person { Id = 1, Name = "Ada" }, options);
        Assert.Equal("A200020163416461", Convert.ToHexString(encoded));
        Assert.Equal(1, CborSerializer.Deserialize<Person>(Convert.FromHexString("A200020163416461"), options).Id);
    }

    [Fact]
    public void ConstructorBoundMembersAreNotAssignedAgain()
    {
        var value = CborSerializer.Deserialize<NormalizedName>(Convert.FromHexString("A10063416461"), Options);
        Assert.Equal("ADA", value.Name);
        Assert.Equal(1, value.Assignments);
    }

    [Fact]
    public void RequiredMembersAreInitializedWithoutOverwritingConstructorNormalization()
    {
        var normalized = CborSerializer.Deserialize<RequiredName>(Convert.FromHexString("A10063416461"), Options);
        Assert.Equal("ADA", normalized.Name);
        var mutable = CborSerializer.Deserialize<RequiredValue>(Convert.FromHexString("A10001"), Options);
        Assert.Equal(1, mutable.Value);
    }

    [Fact]
    public void GeneratedEnumsPreserveSignedUnsignedAndUnknownValues()
    {
        Assert.Equal("1BFFFFFFFFFFFFFFFF", Convert.ToHexString(CborSerializer.Serialize(UnsignedState.All, Options)));
        Assert.Equal(UnsignedState.All, CborSerializer.Deserialize<UnsignedState>(Convert.FromHexString("1BFFFFFFFFFFFFFFFF"), Options));
        Assert.Equal(SignedState.Negative, CborSerializer.Deserialize<SignedState>([0x20], Options));
        Assert.Equal((SignedState)7, CborSerializer.Deserialize<SignedState>([7], Options));
        Assert.Throws<OverflowException>(() => CborSerializer.Deserialize<SignedState>(Convert.FromHexString("190100"), Options));
        Assert.Throws<InvalidDataException>(() => CborSerializer.Deserialize<Dictionary<double, int>>(Convert.FromHexString("A2F9000001F9800002"), Options));
    }

    [Fact]
    public void GeneratedMutableObjectHasStableKnownEncoding()
    {
        var value = new Person { Id = 1, Name = "Ada" };
        byte[] encoded = CborSerializer.Serialize(value, Options);
        Assert.Equal("A200010163416461", Convert.ToHexString(encoded));
        var decoded = CborSerializer.Deserialize<Person>(encoded, Options);
        Assert.Equal(value.Id, decoded.Id);
        Assert.Equal(value.Name, decoded.Name);
        var writer = new ArrayBufferWriter<byte>();
        CborSerializer.Serialize(writer, value, Options);
        Assert.Equal(encoded, writer.WrittenSpan.ToArray());
    }

    [Fact]
    public void GeneratedObjectWorksAcrossEverySegmentSeamAndEmptySegments()
    {
        byte[] encoded = CborSerializer.Serialize(new Person { Id = 42, Name = "水😀" }, Options);
        for (int offset = 0; offset <= encoded.Length; offset++)
        {
            var sequence = IntegerHarness.Split(encoded, offset);
            var value = CborSerializer.Deserialize<Person>(in sequence, Options);
            Assert.Equal(42, value.Id);
            Assert.Equal("水😀", value.Name);
        }

        var fragmented = IntegerHarness.Fragment(encoded);
        Assert.Equal("水😀", CborSerializer.Deserialize<Person>(in fragmented, Options).Name);
    }

    [Fact]
    public void MissingRequiredAndDuplicateKnownMembersFail()
    {
        Assert.Throws<InvalidDataException>(() => CborSerializer.Deserialize<Person>(Convert.FromHexString("A1016141"), Options));
        Assert.Throws<InvalidDataException>(() => CborSerializer.Deserialize<Person>(Convert.FromHexString("A300010002016141"), Options));
    }

    [Theory]
    [InlineData("A30001016341646102820102")]
    [InlineData("BF00010163416461029F0102FFFF")]
    [InlineData("A30001016341646162C3A1820102")]
    public void UnknownMembersAreSkippedWithTheirWholeValue(string hex)
    {
        var value = CborSerializer.Deserialize<Person>(Convert.FromHexString(hex), Options);
        Assert.Equal(1, value.Id);
        Assert.Equal("Ada", value.Name);
    }

    [Fact]
    public void UnknownMembersShareDepthItemsAndEncodingPolicy()
    {
        byte[] input = Convert.FromHexString("A30001016341646102818100");
        Assert.Throws<InvalidDataException>(() => CborSerializer.Deserialize<Person>(input,
            new CborSerializerOptions(TestResolver.Instance, new CborReaderOptions(maxDepth: 2))));
        Assert.Throws<InvalidDataException>(() => CborSerializer.Deserialize<Person>(input,
            new CborSerializerOptions(TestResolver.Instance, new CborReaderOptions(maxItems: 8))));
        Assert.Throws<InvalidDataException>(() => CborSerializer.Deserialize<Person>(Convert.FromHexString("A300010163416461021801"),
            new CborSerializerOptions(TestResolver.Instance, new CborReaderOptions(requirePreferredEncoding: true))));
    }

    [Fact]
    public void UnknownSkipBudgetPersistsAcrossFollowingMembers()
    {
        byte[] input = Convert.FromHexString("A30001028201020163416461");
        Assert.Throws<InvalidDataException>(() => CborSerializer.Deserialize<Person>(input,
            new CborSerializerOptions(TestResolver.Instance, new CborReaderOptions(maxItems: 8))));
        Assert.Equal("Ada", CborSerializer.Deserialize<Person>(input,
            new CborSerializerOptions(TestResolver.Instance, new CborReaderOptions(maxItems: 9))).Name);
    }

    [Fact]
    public void ImmutableConstructorAndStructContractsWork()
    {
        var immutable = new ImmutablePerson(7, "Grace");
        var result = CborSerializer.Deserialize<ImmutablePerson>(CborSerializer.Serialize(immutable, Options), Options);
        Assert.Equal(7, result.Id);
        Assert.Equal("Grace", result.Name);
        var point = new Point { X = -4, Y = 9 };
        Assert.Equal(point, CborSerializer.Deserialize<Point>(CborSerializer.Serialize(point, Options), Options));
        var record = new PersonRecord(3, "Lin");
        Assert.Equal(record, CborSerializer.Deserialize<PersonRecord>(CborSerializer.Serialize(record, Options), Options));
    }

    [Fact]
    public void ConstructorIsNotInvokedForInvalidOrIncompleteObjects()
    {
        ImmutablePerson.Constructions = 0;
        Assert.Throws<InvalidDataException>(() => CborSerializer.Deserialize<ImmutablePerson>(Convert.FromHexString("A0"), Options));
        Assert.Equal(0, ImmutablePerson.Constructions);
        Assert.ThrowsAny<Exception>(() => CborSerializer.Deserialize<ImmutablePerson>(Convert.FromHexString("A20001016341"), Options));
        Assert.Equal(0, ImmutablePerson.Constructions);
    }

    [Fact]
    public void CollectionClosuresAreGeneratedForTheWholeModelGraph()
    {
        var node = new TreeNode { Value = 1, Children = [new TreeNode { Value = 2 }] };
        var decoded = CborSerializer.Deserialize<TreeNode>(CborSerializer.Serialize(node, Options), Options);
        Assert.Equal(2, Assert.Single(decoded.Children!).Value);
        var values = new Dictionary<string, List<Person>> { ["people"] = [new Person { Id = 1, Name = "Ada" }] };
        var decodedMap = CborSerializer.Deserialize<Dictionary<string, List<Person>>>(CborSerializer.Serialize(values, Options), Options);
        Assert.Equal("Ada", Assert.Single(decodedMap["people"]).Name);
        Person[] array = [new Person { Id = 8, Name = "Kay" }];
        Assert.Equal(8, Assert.Single(CborSerializer.Deserialize<Person[]>(CborSerializer.Serialize(array, Options), Options)).Id);
    }

    [Fact]
    public void RecursiveReferenceCyclesFailAtTheWriteDepthLimit()
    {
        var node = new TreeNode();
        node.Children = [node];
        Assert.Throws<InvalidDataException>(() => CborSerializer.Serialize(node,
            new CborSerializerOptions(TestResolver.Instance, new CborReaderOptions(maxDepth: 8))));
    }

    [Theory]
    [InlineData("83010203")]
    [InlineData("9F010203FF")]
    public void ArrayAndListAcceptBothLengthForms(string hex)
    {
        Assert.Equal([1, 2, 3], CborSerializer.Deserialize<int[]>(Convert.FromHexString(hex), Options));
        Assert.Equal([1, 2, 3], CborSerializer.Deserialize<List<int>>(Convert.FromHexString(hex), Options));
    }

    [Fact]
    public void DeclaredLengthCannotForceAnAllocationBeforeBoundsChecks()
    {
        Assert.Throws<InvalidDataException>(() => CborSerializer.Deserialize<int[]>(Convert.FromHexString("9BFFFFFFFFFFFFFFFF"), Options));
        Assert.Throws<InvalidDataException>(() => CborSerializer.Deserialize<List<int>>(Convert.FromHexString("8301"), Options));
        Assert.Throws<InvalidDataException>(() => CborSerializer.Deserialize<int[]>(Convert.FromHexString("9F010203FF"),
            new CborSerializerOptions(TestResolver.Instance, maxCollectionLength: 2)));
        Assert.Throws<InvalidDataException>(() => CborSerializer.Deserialize<int[]>(Convert.FromHexString("80"),
            new CborSerializerOptions(TestResolver.Instance, new CborReaderOptions(maxDepth: 0))));
    }

    [Fact]
    public void NullableDoesNotChargeTheSameItemTwice()
    {
        var options = new CborSerializerOptions(TestResolver.Instance, new CborReaderOptions(maxItems: 1));
        Assert.Equal(7, CborSerializer.Deserialize<int?>([7], options));
        Assert.Null(CborSerializer.Deserialize<int?>([0xf6], options));
        Assert.Equal([7], CborSerializer.Serialize((int?)7, options));
        Assert.Equal([0xf6], CborSerializer.Serialize((int?)null, options));
    }

    [Fact]
    public void NullUndefinedAndTrailingDataHaveDistinctSemantics()
    {
        Assert.Null(CborSerializer.Deserialize<string>([0xf6]));
        Assert.Null(CborSerializer.Deserialize<Person>([0xf6], Options));
        Assert.Throws<InvalidDataException>(() => CborSerializer.Deserialize<string>([0xf7]));
        Assert.Throws<InvalidDataException>(() => CborSerializer.Deserialize<int>([0, 0]));
        Assert.Throws<OverflowException>(() => CborSerializer.Deserialize<byte>(Convert.FromHexString("190100")));
        Assert.Throws<OverflowException>(() => CborSerializer.Deserialize<long>(Convert.FromHexString("1BFFFFFFFFFFFFFFFF")));
        Assert.Throws<OverflowException>(() => CborSerializer.Deserialize<float>(CborSerializer.Serialize(double.MaxValue)));
        Assert.Throws<NotSupportedException>(() => CborSerializer.Serialize(1m));
    }

    [Theory]
    [InlineData("626869")]
    [InlineData("7F61686169FF")]
    public void TextStringsAcceptChunks(string hex)
    {
        var input = Convert.FromHexString(hex);
        Assert.Equal("hi", CborSerializer.Deserialize<string>(input));
        var sequence = IntegerHarness.Fragment(input);
        Assert.Equal("hi", CborSerializer.Deserialize<string>(in sequence));
    }

    [Theory]
    [InlineData("61FF")]
    [InlineData("7F61C261A2FF")]
    [InlineData("7F5F40FFFF")]
    [InlineData("7F7F60FFFF")]
    public void TypedStringReadsRejectInvalidUtf8AndChunks(string hex) =>
        Assert.Throws<InvalidDataException>(() => CborSerializer.Deserialize<string>(Convert.FromHexString(hex)));

    [Fact]
    public void StringBudgetsIncludeEveryChunk()
    {
        var options = new CborSerializerOptions(maxStringLength: 1);
        Assert.Throws<InvalidDataException>(() => CborSerializer.Deserialize<string>(Convert.FromHexString("7F61686169FF"), options));
        Assert.Throws<InvalidDataException>(() => CborSerializer.Serialize("水", options));
        Assert.Equal(string.Empty, CborSerializer.Deserialize<string>(Convert.FromHexString("7FFF"), new CborSerializerOptions(maxStringLength: 0)));
        Assert.Equal([1, 2, 3], CborSerializer.Deserialize<byte[]>(Convert.FromHexString("5F4201024103FF")));
    }

    [Fact]
    public void DictionaryRejectsDuplicateNullAndOddPairs()
    {
        Assert.Throws<InvalidDataException>(() => CborSerializer.Deserialize<Dictionary<string, int>>(Convert.FromHexString("A2616101616102"), Options));
        Assert.Throws<InvalidDataException>(() => CborSerializer.Deserialize<Dictionary<string, int>>(Convert.FromHexString("A1F601"), Options));
        Assert.Throws<InvalidDataException>(() => CborSerializer.Deserialize<Dictionary<string, int>>(Convert.FromHexString("BF6161FF"), Options));
    }

    [Fact]
    public void EveryTruncatedObjectFails()
    {
        byte[] input = CborSerializer.Serialize(new Person { Id = 25, Name = "水😀" }, Options);
        for (int length = 0; length < input.Length; length++)
        {
            byte[] truncated = input.AsSpan(0, length).ToArray();
            Assert.ThrowsAny<Exception>(() => CborSerializer.Deserialize<Person>(truncated, Options));
        }
    }

    [Fact]
    public void ResolverSnapshotsDoNotChangeWhenTheBuilderChanges()
    {
        var registry = new CborFormatterRegistry();
        var oldOptions = new CborSerializerOptions(registry.Build());
        registry.Add(new PlusOneFormatter());
        var newOptions = new CborSerializerOptions(registry.Build());
        Assert.Equal([1], CborSerializer.Serialize(1, oldOptions));
        Assert.Equal([2], CborSerializer.Serialize(1, newOptions));
        Assert.Equal(1, CborSerializer.Deserialize<int>([2], newOptions));
        Assert.Throws<ArgumentException>(() => registry.Add(new PlusOneFormatter()));
        Parallel.For(0, 1000, i => Assert.Equal(i, CborSerializer.Deserialize<int>(CborSerializer.Serialize(i, newOptions), newOptions)));
    }

    private sealed class PlusOneFormatter : ICborFormatter<int>
    {
        public void Serialize<W>(ref W buffer, ref CborSerializationContext context, int value)
            where W : struct, IWriteBuffer
#if NET9_0_OR_GREATER
            , allows ref struct
#endif
            => buffer.WriteInt64(checked(value + 1));

        public int Deserialize<R>(ref R buffer, ref CborDeserializationContext context)
            where R : struct, IReadBuffer
#if NET9_0_OR_GREATER
            , allows ref struct
#endif
            => checked((int)buffer.ReadInt64() - 1);
    }
}

[CborObject]
public sealed class Person
{
    [CborKey(0, Required = true)]
    public int Id { get; set; }

    [CborKey(1)]
    public string? Name { get; set; }
}

[CborObject]
public sealed class ImmutablePerson
{
    public static int Constructions { get; set; }

    [CborConstructor]
    public ImmutablePerson(int id, string? name)
    {
        Constructions++;
        Id = id;
        Name = name;
    }

    [CborKey(0, Required = true)]
    public int Id { get; }

    [CborKey(1)]
    public string? Name { get; }
}

[CborObject]
public record PersonRecord([property: CborKey(0)] int Id, [property: CborKey(1)] string Name);

[CborObject]
public struct Point
{
    [CborKey(0)]
    public int X { get; set; }

    [CborKey(1)]
    public int Y { get; set; }
}

[CborObject]
public sealed class TreeNode
{
    [CborKey(0)]
    public int Value { get; set; }

    [CborKey(1)]
    public List<TreeNode>? Children { get; set; }
}

[CborResolver(typeof(Person), typeof(ImmutablePerson), typeof(PersonRecord), typeof(Point), typeof(TreeNode),
    typeof(Person[]), typeof(Dictionary<string, List<Person>>), typeof(int[]), typeof(List<int>), typeof(int?),
    typeof(Dictionary<string, int>), typeof(UnsignedState), typeof(SignedState), typeof(Dictionary<double, int>), typeof(NormalizedName),
    typeof(RequiredName), typeof(RequiredValue))]
public partial class TestResolver;

public enum UnsignedState : ulong
{
    None = 0,
    All = ulong.MaxValue,
}

public enum SignedState : sbyte
{
    Negative = -1,
    None = 0,
}

[CborObject]
public sealed class NormalizedName
{
    private string name = string.Empty;

    [CborConstructor]
    public NormalizedName(string name)
    {
        Name = name.ToUpperInvariant();
    }

    [CborIgnore]
    public int Assignments { get; private set; }

    [CborKey(0)]
    public string Name
    {
        get => name;
        set
        {
            Assignments++;
            name = value;
        }
    }
}

[CborObject]
public sealed class RequiredName
{
    [CborConstructor, System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
    public RequiredName(string name) => Name = name.ToUpperInvariant();

    [CborKey(0)]
    public required string Name { get; init; }
}

[CborObject]
public sealed class RequiredValue
{
    [CborKey(0)]
    public required int Value { get; init; }
}
