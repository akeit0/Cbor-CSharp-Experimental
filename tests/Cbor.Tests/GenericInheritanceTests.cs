using Cbor.Samples;
using Cbor.Testing;

namespace Cbor.Tests;

public sealed class GenericInheritanceTests
{
    private static readonly CborSerializerOptions Options = new(SampleResolver.Instance);
    private static readonly CborSerializerOptions RequiredOptions = new(RequiredInheritanceResolver.Instance);

    [Fact]
    public void ClosedGenericInstantiationsHaveIndependentTypedFormatters()
    {
        Assert.Equal("A100182A", Convert.ToHexString(CborSerializer.Serialize(new SampleBox<int>(42), Options)));
        Assert.Equal("A10063416461", Convert.ToHexString(CborSerializer.Serialize(new SampleBox<string>("Ada"), Options)));
        Assert.Equal(42, CborSerializer.Deserialize<SampleBox<int>>(Convert.FromHexString("A100182A"), Options).Value);
        Assert.Equal("Ada", CborSerializer.Deserialize<SampleBox<string>>(Convert.FromHexString("A10063416461"), Options).Value);
        Assert.NotNull(SampleResolver.Instance.GetFormatter<SampleBox<int>>());
        Assert.Null(SampleResolver.Instance.GetFormatter<SampleBox<double>>());
    }

    [Fact]
    public void InheritedKeysAndConstructorNormalizationSurviveAllSeams()
    {
        byte[] input = Convert.FromHexString("A300182A01636164610207");
        for (int seam = 0; seam <= input.Length; seam++)
        {
            var source = IntegerHarness.Split(input, seam);
            var value = CborSerializer.Deserialize<SampleDerived>(in source, Options);
            Assert.Equal(42, value.Id);
            Assert.Equal("ADA", value.Label);
            Assert.Equal(7, value.Quantity);
            Assert.Equal(1, value.Assignments);
            Assert.Equal("A300182A01634144410207", Convert.ToHexString(CborSerializer.Serialize(value, Options)));
        }

        var fragmented = IntegerHarness.Fragment(input);
        Assert.Equal("ADA", CborSerializer.Deserialize<SampleDerived>(in fragmented, Options).Label);
    }

    [Theory]
    [InlineData("A201634164610207")]
    [InlineData("A40001000201634144410207")]
    [InlineData("A400010161410161420207")]
    public void InheritedRequiredAndDuplicateSlotsAreEnforced(string hex)
        => Assert.Throws<InvalidDataException>(() => CborSerializer.Deserialize<SampleDerived>(Convert.FromHexString(hex), Options));

    [Fact]
    public void RecursiveGenericGraphsCloseAndUseTheSameLimits()
    {
        var tree = new SampleTree<int>
        {
            Value = 1,
            Children = [new() { Value = 2, Children = [] }, new() { Value = 3 }],
        };
        byte[] encoded = CborSerializer.Serialize(tree, Options);
        var source = IntegerHarness.Fragment(encoded);
        var decoded = CborSerializer.Deserialize<SampleTree<int>>(in source, Options);
        Assert.Equal(1, decoded.Value);
        Assert.Equal([2, 3], decoded.Children!.Select(static node => node.Value));
        Assert.Empty(decoded.Children![0].Children!);
        Assert.Null(decoded.Children[1].Children);
        var bounded = new CborSerializerOptions(SampleResolver.Instance, new CborReaderOptions(maxDepth: 2));
        Assert.Throws<InvalidDataException>(() => CborSerializer.Deserialize<SampleTree<int>>(encoded, bounded));
        Assert.Throws<InvalidDataException>(() => CborSerializer.Serialize(tree, bounded));
    }

    [Fact]
    public void InheritedClrRequiredMembersUseAnInitializerOrTheirSelectedConstructor()
    {
        var mutable = CborSerializer.Deserialize<MutableRequired>(Convert.FromHexString("A10007"), RequiredOptions);
        Assert.Equal(7, mutable.Value);
        var normalized = CborSerializer.Deserialize<ConstructorRequired>(Convert.FromHexString("A10063616461"), RequiredOptions);
        Assert.Equal("ADA", normalized.Value);
        Assert.Throws<InvalidDataException>(() => CborSerializer.Deserialize<MutableRequired>([0xa0], RequiredOptions));
    }

    [Fact]
    public void ClosedGenericStructsAndInheritedRecordsUseTheirConstructors()
    {
        var pair = new GenericPair<int>(1, 2);
        Assert.Equal(pair, CborSerializer.Deserialize<GenericPair<int>>(CborSerializer.Serialize(pair, RequiredOptions), RequiredOptions));
        var record = new DerivedRecord(42, "Ada");
        Assert.Equal("A200182A0163416461", Convert.ToHexString(CborSerializer.Serialize(record, RequiredOptions)));
        Assert.Equal(record, CborSerializer.Deserialize<DerivedRecord>(CborSerializer.Serialize(record, RequiredOptions), RequiredOptions));
    }
}

[CborObject]
public abstract class RequiredBase<T>
{
    [CborKey(0, Required = true)]
    public required T Value { get; init; }
}

[CborObject]
public sealed class MutableRequired : RequiredBase<int>;

[CborObject]
public sealed class ConstructorRequired : RequiredBase<string>
{
    [CborConstructor, System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
    public ConstructorRequired(string value) => Value = value.ToUpperInvariant();
}

[CborObject]
public readonly record struct GenericPair<T>([property: CborKey(0)] T First, [property: CborKey(1)] T Second);

[CborObject]
public record BaseRecord([property: CborKey(0)] int Id);

[CborObject]
public sealed record DerivedRecord(int Id, [property: CborKey(1)] string Name) : BaseRecord(Id);

[CborResolver(typeof(MutableRequired), typeof(ConstructorRequired), typeof(GenericPair<int>), typeof(DerivedRecord))]
public partial class RequiredInheritanceResolver;
