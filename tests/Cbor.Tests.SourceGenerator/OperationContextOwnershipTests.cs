using System.Collections.Immutable;
using Cbor.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Cbor.Tests.SourceGenerator;

public sealed class OperationContextOwnershipTests
{
    private static readonly ImmutableArray<MetadataReference> References =
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
        .Select(static path => (MetadataReference)MetadataReference.CreateFromFile(path)).ToImmutableArray();

    [Theory]
    [InlineData("void M() { var a = new Context(); var b = a; }")]
    [InlineData("void M(ref Context a, ref Context b) { b = a; }")]
    [InlineData("void M(ref Context a) { object b = a; }")]
    [InlineData("void M(ref Context a) { System.IDisposable b = a; }")]
    [InlineData("void M(ref Context a) { Context? b = a; }")]
    [InlineData("void M(ref Context a) { var b = (a, 1); }")]
    [InlineData("void M(ref Context a) { var b = new[] { a }; }")]
    [InlineData("void M(ref Context a) { if (a is Context b) { } }")]
    [InlineData("void M(ref Context a) { var b = a with { }; }")]
    [InlineData("void M(ref Context a) { using (a) { } }")]
    [InlineData("void M(ref Context a) { using var b = a; }")]
    [InlineData("void M(Context a) { }")]
    [InlineData("void M(in Context a) { }")]
    [InlineData("delegate void M(Context a);")]
    [InlineData("Context a; Context M() => a;")]
    [InlineData("Context a; Context Value => a;")]
    [InlineData("Context M() { using var a = new Context(); return a; }")]
    [InlineData("readonly Context a; void M() { a.Dispose(); }")]
    [InlineData("System.Collections.Generic.IEnumerable<Context> M() { var a = new Context(); yield return a; }")]
    [InlineData("void M(Context[] values) { foreach (var a in values) { } }")]
    public async Task OwnershipCopiesAreErrors(string member)
    {
        var diagnostics = await Analyze(member);
        Assert.NotEmpty(diagnostics);
        Assert.All(diagnostics, static diagnostic =>
        {
            Assert.Equal("CBOR003", diagnostic.Id);
            Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        });
    }

    [Theory]
    [InlineData("void M() { var a = new Context(); try { a.Touch(); } finally { a.Dispose(); } }")]
    [InlineData("void M(ref Context a) { a.Touch(); }")]
    [InlineData("void M(out Context a) { a = new Context(); }")]
    [InlineData("Context M() => new Context();")]
    [InlineData("Context M() { var a = new Context(); return a; }")]
    [InlineData("void M() { var a = M2(); } Context M2() => new Context();")]
    [InlineData("Context a; ref Context M() => ref a;")]
    [InlineData("void M(ref Context a) { ref Context b = ref a; b.Touch(); }")]
    [InlineData("void M() { using var a = new Context(); }")]
    [InlineData("readonly Context a; int M() => a.Read();")]
    [InlineData("System.Collections.Generic.IEnumerable<Context> M() { yield return new Context(); }")]
    public async Task FreshOwnershipAndMutableReferencesAreAllowed(string member)
    {
        Assert.Empty(await Analyze(member));
    }

    [Fact]
    public async Task BothContextTypesAreRecognized()
    {
        Assert.Single(await Analyze("void M() { var a = new Cbor.CborDeserializationContext(); var b = a; }"));
    }

    private static async Task<ImmutableArray<Diagnostic>> Analyze(string member)
    {
        string source = """
            using Context = Cbor.CborSerializationContext;
            namespace Cbor {
                public struct CborSerializationContext : System.IDisposable {
                    public void Dispose() { }
                    public void Touch() { }
                    public readonly int Read() => 0;
                }
                public struct CborDeserializationContext : System.IDisposable {
                    public void Dispose() { }
                }
            }
            public class Test {
            """ + member + "}";
        var compilation = CSharpCompilation.Create("ContextOwnership",
            [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.CSharp10))], References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        Assert.Empty(compilation.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        return await compilation.WithAnalyzers([new OperationContextOwnershipAnalyzer()]).GetAnalyzerDiagnosticsAsync();
    }
}
