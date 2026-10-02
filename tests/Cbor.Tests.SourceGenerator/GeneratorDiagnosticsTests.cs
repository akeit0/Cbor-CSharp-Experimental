using System.Collections.Immutable;
using Cbor.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Cbor.Tests.SourceGenerator;

public sealed class GeneratorDiagnosticsTests
{
    [Theory]
    [InlineData("private int Provider;")]
    [InlineData("private class GeneratedFactory { }")]
    public void GeneratedFactoryMembersCannotCollideWithUserDeclarations(string member)
    {
        var result = Run("using Cbor; [CborResolver(typeof(int))] public partial class Resolver { " + member + " }");
        Assert.Null(result.Exception);
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Id == "CBOR001");
        Assert.Empty(result.GeneratedSources);
    }

    [Theory]
    [InlineData("private int ObjectFormatter0;")]
    [InlineData("private class ObjectFormatter0<W, R> { }")]
    public void GeneratedFormatterMembersCannotCollideWithUserDeclarations(string member)
    {
        var result = Run("using Cbor; [CborObject] public class Model { } [CborResolver(typeof(Model))] public partial class Resolver { " + member + " }");
        Assert.Null(result.Exception);
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Id == "CBOR001");
        Assert.Empty(result.GeneratedSources);
    }
    private static readonly ImmutableArray<MetadataReference> References = CreateReferences();

    [Fact]
    public void HalfRootsAreRejectedWhenTheReferencedRuntimeDoesNotSupportHalf()
    {
        var parse = new CSharpParseOptions(LanguageVersion.CSharp10);
        var tree = CSharpSyntaxTree.ParseText("""
            namespace Cbor {
                [System.AttributeUsage(System.AttributeTargets.Class)]
                public sealed class CborResolverAttribute : System.Attribute {
                    public CborResolverAttribute(params System.Type[] roots) { }
                }
            }
            [Cbor.CborResolver(typeof(System.Half))] public partial class Resolver { }
            """, parse);
        var references = References.Where(reference => reference.Display != typeof(CborObjectAttribute).Assembly.Location);
        var compilation = CSharpCompilation.Create("LegacyRuntime", [tree], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        GeneratorDriver driver = CSharpGeneratorDriver.Create([new CborGenerator().AsSourceGenerator()], parseOptions: parse);
        var result = driver.RunGenerators(compilation).GetRunResult().Results.Single();
        Assert.Null(result.Exception);
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Id == "CBOR002");
        Assert.Empty(result.GeneratedSources);
    }

    [Fact]
    public void GeneratedCodeCompilesForAMutableAndConstructorBoundModel()
    {
        var parse = new CSharpParseOptions(LanguageVersion.CSharp10);
        var tree = CSharpSyntaxTree.ParseText("""
            using Cbor;
            [CborObject] public class Model {
                [CborKey(0)] public int Value { get; set; }
                [CborConstructor] public Model(int value) { Value = value; }
            }
            [CborResolver(typeof(Model))] public partial class Resolver { }
            """, parse);
        var compilation = CSharpCompilation.Create("CompiledGeneration", [tree], References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        GeneratorDriver driver = CSharpGeneratorDriver.Create([new CborGenerator().AsSourceGenerator()], parseOptions: parse);
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var generatedCompilation, out var diagnostics);
        Assert.Empty(diagnostics);
        using var stream = new MemoryStream();
        var result = generatedCompilation.Emit(stream);
        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
    }

    [Fact]
    public void ResolverHintNamesCannotCollideAcrossNamespaceSeparators()
    {
        var result = Run("""
            using Cbor;
            namespace A.B_C { [CborResolver(typeof(int))] public partial class Resolver { } }
            namespace A_B.C { [CborResolver(typeof(int))] public partial class Resolver { } }
            """);
        Assert.Null(result.Exception);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(2, result.GeneratedSources.Length);
    }

    [Fact]
    public void ResolverWithNoParameterlessConstructorIsDiagnosed()
    {
        var result = Run("using Cbor; [CborResolver(typeof(int))] public partial class Resolver { public Resolver(int value) { } }");
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Id == "CBOR001");
        Assert.Empty(result.GeneratedSources);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("new System.Type[] { null }")]
    public void NullResolverRootsProduceDiagnosticsInsteadOfCrashing(string roots)
    {
        var result = Run("using Cbor; [CborResolver(" + roots + ")] public partial class Resolver { }");
        Assert.Null(result.Exception);
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Id == "CBOR001");
        Assert.Empty(result.GeneratedSources);
    }

    [Theory]
    [InlineData("byte[]")]
    [InlineData("System.Tuple<int>")]
    [InlineData("Cbor.CborInteger")]
    [InlineData("Cbor.CborSimpleValue")]
    [InlineData("System.Numerics.BigInteger")]
    [InlineData("System.Half")]
    public void UnsupportedDictionaryKeysAreDiagnosedBeforeResolverInitialization(string key)
    {
        var result = Run("using Cbor; [CborResolver(typeof(System.Collections.Generic.Dictionary<" + key + ", int>))] public partial class Resolver { }");
        Assert.Null(result.Exception);
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Id == "CBOR002");
        Assert.Empty(result.GeneratedSources);
    }

    [Fact]
    public void InternalGetterInAnotherAssemblyIsNotAccessibleToTheGeneratedFormatter()
    {
        var parse = new CSharpParseOptions(LanguageVersion.CSharp10);
        var model = CSharpCompilation.Create("ExternalModel", [CSharpSyntaxTree.ParseText("""
            using Cbor;
            [CborObject] public class Model {
                [CborKey(0)] public int Value { internal get; set; }
            }
            """, parse)], References, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var stream = new MemoryStream();
        var emit = model.Emit(stream);
        Assert.True(emit.Success, string.Join(Environment.NewLine, emit.Diagnostics));
        var compilation = CSharpCompilation.Create("Generation", [CSharpSyntaxTree.ParseText(
            "using Cbor; [CborResolver(typeof(Model))] public partial class Resolver { }", parse)],
            References.Add(MetadataReference.CreateFromImage(stream.ToArray())));
        GeneratorDriver driver = CSharpGeneratorDriver.Create([new CborGenerator().AsSourceGenerator()], parseOptions: parse);
        var result = driver.RunGenerators(compilation).GetRunResult().Results.Single();
        Assert.Null(result.Exception);
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Id == "CBOR001" && diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture).Contains("getter", StringComparison.Ordinal));
        Assert.Empty(result.GeneratedSources);
    }

    [Fact]
    public void ConstructorBoundRequiredMetadataNeedsSetsRequiredMembers()
    {
        var result = Run("""
            using Cbor;
            using System.Runtime.CompilerServices;
            [CborObject] public class Model {
                [CborKey(0), RequiredMember] public int Value { get; set; }
                [CborConstructor] public Model(int value) { Value = value; }
            }
            [CborResolver(typeof(Model))] public partial class Resolver { }
            """);
        Assert.Null(result.Exception);
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Id == "CBOR001" && diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture).Contains("SetsRequiredMembers", StringComparison.Ordinal));
        Assert.Empty(result.GeneratedSources);
    }

    [Theory]
    [InlineData("public class Model { public int Value { get; set; } }", "CBOR001")]
    [InlineData("public class Model { [CborKey(0)] public int A { get; set; } [CborKey(0)] public int B { get; set; } }", "CBOR001")]
    [InlineData("public class Model { [CborKey(-1)] public int A { get; set; } }", "CBOR001")]
    [InlineData("public class Model { [CborKey(0), CborIgnore] public int A { get; set; } }", "CBOR001")]
    [InlineData("public class Model { [CborKey(0)] public int A { get; } }", "CBOR001")]
    [InlineData("public class Model { [CborKey(0)] public static int A { get; set; } }", "CBOR001")]
    [InlineData("public class Model { [CborKey(0)] public decimal A { get; set; } }", "CBOR002")]
    [InlineData("public class Model { [CborKey(0)] public int[,] A { get; set; } }", "CBOR002")]
    [InlineData("public abstract class Model { }", "CBOR001")]
    [InlineData("public class Model : Parent { } public class Parent { }", "CBOR001")]
    [InlineData("public class Model { [CborKey(0)] public int A { get; } [CborConstructor] private Model(int a) { A=a; } }", "CBOR001")]
    [InlineData("public class Model { [CborConstructor] public Model() { } [CborConstructor] public Model(int a) { } }", "CBOR001")]
    public void InvalidContractsProduceSpecificErrorsAndNoGeneratedResolver(string model, string expected)
    {
        var result = Run("using Cbor; [CborObject] " + model + " [CborResolver(typeof(Model))] public partial class Resolver { }");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == expected && diagnostic.Severity == DiagnosticSeverity.Error);
        Assert.Empty(result.GeneratedSources);
        Assert.Null(result.Exception);
    }

    [Theory]
    [InlineData("public class Resolver")]
    [InlineData("public abstract partial class Resolver")]
    [InlineData("public static partial class Resolver")]
    [InlineData("public partial class Resolver<T>")]
    public void InvalidResolverDeclarationsAreDiagnosed(string declaration)
    {
        var result = Run("using Cbor; [CborResolver(typeof(int))] " + declaration + " { }");
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Id == "CBOR001");
        Assert.Null(result.Exception);
    }

    [Fact]
    public void RecursiveGraphTerminatesAndGeneratesClosedCollectionFormatters()
    {
        var result = Run("""
            using Cbor;
            using System.Collections.Generic;
            [CborObject] public class Node {
                [CborKey(0)] public List<Node> Children { get; set; }
                [CborKey(1)] public int? Value { get; set; }
            }
            [CborResolver(typeof(Node))] public partial class Resolver { }
            """);
        Assert.Empty(result.Diagnostics);
        Assert.Null(result.Exception);
        string generated = Assert.Single(result.GeneratedSources).SourceText.ToString();
        Assert.Contains("CborListFormatter<W, R, global::Node>", generated);
        Assert.Contains("CborNullableFormatter<W, R, int>", generated);
        Assert.Contains("Initialize(global::Cbor.CborFormatterResolver resolver)", generated);
        Assert.DoesNotContain("context.GetRequiredFormatter", generated);
        Assert.DoesNotContain("MakeGenericType", generated);
        Assert.DoesNotContain("System.Reflection", generated);
    }

    [Fact]
    public void GeneratorRespondsToAnEditedWireContractWithoutStaleOutput()
    {
        const string prefix = "using Cbor; [CborObject] public class Model { [CborKey(";
        const string suffix = ")] public int A { get; set; } } [CborResolver(typeof(Model))] public partial class Resolver { }";
        var parse = new CSharpParseOptions(LanguageVersion.CSharp10);
        var initial = CSharpCompilation.Create("Generation", [CSharpSyntaxTree.ParseText(prefix + "0" + suffix, parse)], References);
        GeneratorDriver driver = CSharpGeneratorDriver.Create([new CborGenerator().AsSourceGenerator()], parseOptions: parse);
        driver = driver.RunGenerators(initial);
        string before = Assert.Single(driver.GetRunResult().Results[0].GeneratedSources).SourceText.ToString();
        var changed = initial.ReplaceSyntaxTree(initial.SyntaxTrees.Single(), CSharpSyntaxTree.ParseText(prefix + "9" + suffix, parse));
        driver = driver.RunGenerators(changed);
        string after = Assert.Single(driver.GetRunResult().Results[0].GeneratedSources).SourceText.ToString();
        Assert.Contains("case 0:", before);
        Assert.Contains("case 9:", after);
        Assert.DoesNotContain("case 0:", after);
        Assert.Empty(driver.GetRunResult().Diagnostics);
    }

    private static GeneratorRunResult Run(string source)
    {
        var parse = new CSharpParseOptions(LanguageVersion.CSharp10);
        var compilation = CSharpCompilation.Create("Generation", [CSharpSyntaxTree.ParseText(source, parse)], References);
        GeneratorDriver driver = CSharpGeneratorDriver.Create([new CborGenerator().AsSourceGenerator()], parseOptions: parse);
        return driver.RunGenerators(compilation).GetRunResult().Results.Single();
    }

    private static ImmutableArray<MetadataReference> CreateReferences()
    {
        string[] platform = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator);
        return platform.Append(typeof(CborObjectAttribute).Assembly.Location).Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(static path => (MetadataReference)MetadataReference.CreateFromFile(path)).ToImmutableArray();
    }
}
