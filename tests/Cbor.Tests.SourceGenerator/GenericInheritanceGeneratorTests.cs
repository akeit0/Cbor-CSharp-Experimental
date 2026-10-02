using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Cbor.SourceGenerator;

namespace Cbor.Tests.SourceGenerator;

public sealed class GenericInheritanceGeneratorTests
{
    private static readonly ImmutableArray<MetadataReference> References = CreateReferences();

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ExternalGenericBaseUsesMetadataSubstitutionAndRealAccessorAccessibility(bool accessibleGetter)
    {
        string property = accessibleGetter ? "public T Value { get; }" : "public T Value { internal get; set; }";
        var external = CreateCompilation("using Cbor; [CborObject] public class ExternalBase<T> { [CborKey(0)] " + property +
            " protected ExternalBase(T value) { Value=value; } }");
        using var image = new MemoryStream();
        var emit = external.Emit(image);
        Assert.True(emit.Success, string.Join(Environment.NewLine, emit.Diagnostics));
        var reference = MetadataReference.CreateFromImage(image.ToArray());
        var compilation = CreateCompilation("using Cbor; [CborObject] public class Derived<T> : ExternalBase<T> { " +
            "[CborConstructor] public Derived(T value) : base(value) { } } " +
            "[CborResolver(typeof(Derived<int>))] public partial class Resolver { }").AddReferences(reference);
        GeneratorDriver driver = CSharpGeneratorDriver.Create([new CborGenerator().AsSourceGenerator()],
            parseOptions: (CSharpParseOptions)compilation.SyntaxTrees.Single().Options);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var generated, out var diagnostics);
        Assert.Null(driver.GetRunResult().Results.Single().Exception);
        if (accessibleGetter)
        {
            Assert.Empty(diagnostics);
            using var result = new MemoryStream();
            var generatedEmit = generated.Emit(result);
            Assert.True(generatedEmit.Success, string.Join(Environment.NewLine, generatedEmit.Diagnostics));
        }
        else
        {
            Assert.Contains(diagnostics, static diagnostic => diagnostic.Id == "CBOR001" &&
                diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture).Contains("getter", StringComparison.Ordinal));
            Assert.Empty(driver.GetRunResult().Results.Single().GeneratedSources);
        }
    }

    [Theory]
    [InlineData("class Box<T> { [CborKey(0)] public T Value { get; set; } }", "Box<int>")]
    [InlineData("struct Box<T> { [CborKey(0)] public T Value { get; } [CborConstructor] public Box(T value) { Value=value; } }", "Box<string>")]
    [InlineData("record Box<T>([property: CborKey(0)] T Value);", "Box<int>")]
    public void ClosedGenericKindsCompileWithSubstitutedConstructors(string model, string root)
    {
        Compile("using Cbor; [CborObject] public " + model + " [CborResolver(typeof(" + root + "))] public partial class Resolver { }");
    }

    [Fact]
    public void GenericContainingTypesAndIndependentClosedInstantiationsCompile()
    {
        Compile("""
            using Cbor;
            public class Outer<T> {
                [CborObject] public class Inner<U> {
                    [CborKey(0)] public T First { get; set; }
                    [CborKey(1)] public U Second { get; set; }
                }
            }
            [CborResolver(typeof(Outer<int>.Inner<string>), typeof(Outer<string>.Inner<long>))]
            public partial class Resolver { }
            """);
    }

    [Fact]
    public void RecursiveAndPermutedGenericGraphsCloseWithoutReflection()
    {
        string source = Compile("""
            using Cbor;
            using System.Collections.Generic;
            [CborObject] public class Node<T> {
                [CborKey(0)] public T Value { get; set; }
                [CborKey(1)] public List<Node<T>> Children { get; set; }
            }
            [CborObject] public class Pair<A,B> {
                [CborKey(0)] public A First { get; set; }
                [CborKey(1)] public B Second { get; set; }
                [CborKey(2)] public Pair<B,A> Next { get; set; }
            }
            [CborResolver(typeof(Node<int>), typeof(Pair<int,string>))] public partial class Resolver { }
            """);
        Assert.Contains("global::Node<int>", source);
        Assert.Contains("global::Pair<string, int>", source);
        Assert.DoesNotContain("MakeGenericType", source);
    }

    [Fact]
    public void FiniteConstructedTypeDependenciesAreNotMistakenForExpandingRecursion()
    {
        Compile("""
            using Cbor;
            using System.Collections.Generic;
            [CborObject] public class Node<T> {
                [CborKey(0)] public Node<List<int>> Fixed { get; set; }
            }
            [CborResolver(typeof(Node<int>))] public partial class Resolver { }
            """);
    }

    [Theory]
    [InlineData("Box<>")]
    [InlineData("System.Collections.Generic.List<>")]
    public void OpenGenericRootsAreDiagnosed(string root)
    {
        var result = Run("using Cbor; [CborObject] public class Box<T> { } [CborResolver(typeof(" + root + "))] public partial class Resolver { }");
        Assert.Null(result.Exception);
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Id == "CBOR001" &&
            diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture).Contains("closed types", StringComparison.Ordinal));
        Assert.Empty(result.GeneratedSources);
    }

    [Theory]
    [InlineData("System.Collections.Generic.List<T>")]
    [InlineData("System.Tuple<T,T>")]
    public void ExpandingGenericGraphProducesABoundedDiagnostic(string argument)
    {
        var result = Run("using Cbor; [CborObject] public class Node<T> { [CborKey(0)] public Node<" + argument +
            "> Child { get; set; } } [CborResolver(typeof(Node<int>))] public partial class Resolver { }");
        Assert.Null(result.Exception);
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Id == "CBOR001" &&
            diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture).Contains("expanding", StringComparison.Ordinal));
        Assert.Empty(result.GeneratedSources);
    }

    [Fact]
    public void GenericAbstractBaseAndMultiLevelOverridesCompile()
    {
        string generated = Compile("""
            using Cbor;
            [CborObject] public abstract class Base<T> {
                [CborKey(0, Required=true)] public abstract T Value { get; set; }
            }
            [CborObject] public abstract class Middle<T> : Base<T> {
                public override T Value { get; set; }
            }
            [CborObject] public class Derived : Middle<int> {
                public override int Value { get; set; }
                [CborKey(1)] public string Label { get; set; }
            }
            [CborResolver(typeof(Derived))] public partial class Resolver { }
            """);
        Assert.Contains("buffer.WriteMapHeader(2UL)", generated);
        Assert.Contains("A required CBOR object member is missing", generated);
    }

    [Theory]
    [InlineData("[CborKey(0)] public int Other { get; set; }", "unique")]
    [InlineData("[CborKey(1)] public override int Value { get; set; }", "override")]
    [InlineData("[CborIgnore] public override int Value { get; set; }", "override")]
    [InlineData("[CborKey(0)] public override int Value { get; set; }", "Required")]
    [InlineData("[CborKey(1)] public new int Value { get; set; }", "hide")]
    [InlineData("[CborIgnore] public new int Value { get; set; }", "hide")]
    [InlineData("public new int Value() => 0;", "hide")]
    [InlineData("public new static int Value;", "hide")]
    public void ConflictingInheritedContractsAreDiagnosed(string member, string message)
    {
        var result = Run("using Cbor; [CborObject] public class Base { [CborKey(0, Required=true)] public virtual int Value { get; set; } } " +
            "[CborObject] public class Derived : Base { " + member + " } [CborResolver(typeof(Derived))] public partial class Resolver { }");
        Assert.Null(result.Exception);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CBOR001" &&
            diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture).Contains(message, StringComparison.Ordinal));
        Assert.Empty(result.GeneratedSources);
    }

    [Fact]
    public void UnannotatedIntermediateBaseIsRejected()
    {
        var result = Run("""
            using Cbor;
            [CborObject] public class Base { [CborKey(0)] public int Value { get; set; } }
            public class Middle : Base { }
            [CborObject] public class Derived : Middle { }
            [CborResolver(typeof(Derived))] public partial class Resolver { }
            """);
        Assert.Null(result.Exception);
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Id == "CBOR001" &&
            diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture).Contains("base class", StringComparison.Ordinal));
        Assert.Empty(result.GeneratedSources);
    }

    [Fact]
    public void InheritedRequiredMembersNeedAnInitializerOrSetsRequiredMembers()
    {
        var result = Run("""
            using Cbor;
            using System.Runtime.CompilerServices;
            [CborObject] public class Base { [CborIgnore, RequiredMember] public int Value { get; set; } }
            [CborObject] public class Derived : Base { }
            [CborResolver(typeof(Derived))] public partial class Resolver { }
            """);
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Id == "CBOR001" &&
            diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture).Contains("SetsRequiredMembers", StringComparison.Ordinal));
        Assert.Empty(result.GeneratedSources);
    }

    private static string Compile(string source)
    {
        var compilation = CreateCompilation(source);
        var parse = (CSharpParseOptions)compilation.SyntaxTrees.Single().Options;
        GeneratorDriver driver = CSharpGeneratorDriver.Create([new CborGenerator().AsSourceGenerator()], parseOptions: parse);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var generated, out var diagnostics);
        Assert.Empty(diagnostics);
        Assert.Null(driver.GetRunResult().Results.Single().Exception);
        using var stream = new MemoryStream();
        var result = generated.Emit(stream);
        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        return string.Join("\n", driver.GetRunResult().Results.Single().GeneratedSources.Select(static item => item.SourceText.ToString()));
    }

    private static GeneratorRunResult Run(string source)
    {
        var compilation = CreateCompilation(source);
        GeneratorDriver driver = CSharpGeneratorDriver.Create([new CborGenerator().AsSourceGenerator()],
            parseOptions: (CSharpParseOptions)compilation.SyntaxTrees.Single().Options);
        return driver.RunGenerators(compilation).GetRunResult().Results.Single();
    }

    private static CSharpCompilation CreateCompilation(string source) => CSharpCompilation.Create("GenericInheritance",
        [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.CSharp10))], References,
        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

    private static ImmutableArray<MetadataReference> CreateReferences()
    {
        string[] platform = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator);
        return platform.Append(typeof(CborObjectAttribute).Assembly.Location).Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(static path => (MetadataReference)MetadataReference.CreateFromFile(path)).ToImmutableArray();
    }
}
