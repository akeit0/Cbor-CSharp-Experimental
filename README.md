# Cbor-CSharp

A CBOR serializer for .NET, informed by MessagePack-CSharp v4 and built on SerializerFoundation 1.0.0. The goal is a complete serializer with comparable correctness, performance, tooling, and long-term maintainability.

**Status: experimental typed serialization and source generation.** CborSerializer serializes CLR values and generated object contracts directly through Foundation buffers. The runtime includes scalar/string/collection formatters, immutable resolver selection, shared resource budgets, and generated mutable/immutable object maps. Validation supports the serializer and low-level callers. Full MessagePack-level feature and performance parity has not been established.

## Solution

| Project | Purpose | Targets |
| --- | --- | --- |
| Cbor | Typed serializer, formatters/resolvers, primitive APIs, bounded readers/writers | netstandard2.0, netstandard2.1, net8.0, net9.0, net10.0 |
| Cbor.SourceGenerator | Incremental object/enum/collection generation, contract and context ownership diagnostics | netstandard2.0 |
| Cbor.SourceGenerator.CodeFixes | Future IDE fixes, isolated Workspaces dependencies; currently empty | netstandard2.0 |
| Cbor.Tests | RFC vectors, truncation/seam checks, exhaustive half patterns, buffer behavior | net8.0, net9.0, net10.0 |
| Cbor.Tests.NetStandard20 | Same tests executed against the oldest library asset | net8.0 consumer, netstandard2.0 library |
| Cbor.Tests.NetStandard21 | Same tests forced onto the legacy netstandard2.1 asset | net8.0 consumer, netstandard2.1 library |
| Cbor.Tests.SourceGenerator | Invalid contracts, recursive graphs, incremental contract edits | net10.0 |
| Cbor.Tests.Conformance | Independent oracle, generated nested items/mutations, numeric/text interop | net10.0 |
| Cbor.Tests.Robustness | Resource limits, malformed inputs, encoding policies | net10.0 |
| Cbor.Tests.NativeAot | Generated models, immutable constructors, dictionaries, RFC corpus, segmented input | net10.0 |
| Cbor.Benchmarks | Primitive/oracle comparisons, typed workloads, scalar disassembly | net10.0 |
| Cbor.Benchmarks.Net8 | Dictionary insertion with .NET 8 APIs and Compatible buffers | net8.0 |
| Cbor.Benchmarks.NetStandard20 | Nested models and dictionary paths against the oldest runtime asset | net8.0 consumer, netstandard2.0 library |
| Cbor.Sandbox | Runnable generated-object serialization example | net10.0 |

The .NET 9 boundary matters: generic code can accept Foundation ref-struct buffers there. The .NET Standard assets use its Compatible buffers. The .NET 8 asset also uses Compatible buffers and uses supported entry-reference dictionary insertion. Separate test projects force both .NET Standard assets so legacy coverage is retained.

## Develop

Install the stable .NET SDK specified by global.json. To execute all tests locally, also install .NET 8 and .NET 9 runtimes.

```powershell
dotnet restore Cbor.slnx --locked-mode
dotnet build Cbor.slnx -c Release --no-restore
dotnet test Cbor.slnx -c Release --no-build
pwsh -File eng/verify.ps1
dotnet run --project sandbox/Cbor.Sandbox -c Release
dotnet run --project benchmarks/Cbor.Benchmarks -c Release -- --filter "*UnsignedInteger*"
dotnet run --project benchmarks/Cbor.Benchmarks -c Release -- --filter "*TypedPath*" "*ScalarRead*" "*ScalarWrite*"
```

The verification script also checks whitespace, packs/inspects the experimental runtime package, installs it into a fresh cache, executes .NET 8/9/10 consumers, and verifies that transitive SF002 rejects a buffer copy and CBOR003 rejects an operation context copy. Ordinary dependencies are centrally versioned and locked. AOT toolchain dependencies and the changing local-package consumer deliberately do not use lock files.

Finish builds/tests before starting benchmarks, then let the benchmark process finish before rebuilding its assemblies.

`eng/verify-native.ps1 -RuntimeIdentifier win-x64` (or `linux-x64` on Linux) runs the project-based native corpus, creates a fresh package, and publishes/executes an independent Native AOT package consumer. Both native paths treat trim/AOT warnings as errors.

Read [the critical quality review](docs/quality-review.md) for remaining quality and performance gaps and required work.
See [operation resolution measurements](docs/benchmarks/operation-resolution.md) for nested workloads and the compatibility dictionary evaluation.
See [runtime path measurements](docs/benchmarks/runtime-paths.md) for object, map, and UTF-8 workloads and reproduction instructions.

## Typed API

```csharp
using Cbor;

var options = new CborSerializerOptions(AppResolver.Instance);
byte[] bytes = CborSerializer.Serialize(new Person { Id = 1, Name = "Ada" }, options);
Person person = CborSerializer.Deserialize<Person>(bytes, options);

[CborObject]
public sealed class Person
{
    [CborKey(0, Required = true)]
    public int Id { get; set; }

    [CborKey(1)]
    public string? Name { get; set; }
}

[CborResolver(typeof(Person), typeof(List<Person>))]
public partial class AppResolver;
```

Objects use stable integer-keyed maps. Every new public instance slot needs CborKey or CborIgnore; property overrides retain their inherited slot contracts. Generated readers reject repeated known keys and missing required members; unknown members are skipped with the same budgets and encoding policy. Missing optional members receive default(T). An accessible constructor can bind readonly members by name/type; CborConstructor selects one explicitly. Records, structs, closed generic and inherited models, nullable values, enums, arrays, lists, dictionaries, and recursive closed model graphs are supported. Model bases explicitly declare CborObject; hiding keyed members or reusing keys across a hierarchy produces diagnostics.

Resolver roots are explicit for Native AOT: the generator closes the entire reachable formatter graph at compile time. There is no assembly scanning, runtime generic construction, or reflection fallback. Formatters resolve each used child type once per operation through context.GetRequiredFormatter\<T\>(); repeated nested objects share the same selection. Custom thread-safe ICborFormatter\<T\> implementations can be registered through CborFormatterRegistry and supplied in an ordered CborCompositeResolver. Unsupported generated contracts fail with CBOR001/CBOR002 instead of silently losing members.

Serialize overloads accept a value, an IBufferWriter\<byte\>, or a borrowed Foundation buffer. Deserialize accepts a span, segmented sequence, or bounded Foundation buffer and rejects trailing bytes. Null and undefined remain distinct. Definite and indefinite strings/collections are accepted by default; string chunks must individually contain valid UTF-8. Dictionary readers reject duplicate CLR keys and use process-keyed hashing for supported key types.

Operation contexts own budgets and pooled formatter storage. Custom formatters borrow them by ref and must not dispose them. Direct context callers must dispose in finally; CBOR003 diagnoses ownership copies and boxing.

Read [the typed serializer design](docs/design/typed-serializer.md) for the exact contract, limits, and remaining work. Broad tagged CLR built-ins, unions/reference preservation, deterministic dictionary ordering, outer async streaming, and IDE fixes remain unfinished. These are serializer work, not reasons to redefine the project as a validator.

## Low-level API

CborPrimitives provides allocation-free span operations. Foundation buffers gain WriteInt64, WriteDouble, WriteTextString, ReadArrayHeader, ReadTextString, and other CBOR extensions. CborValidation.TryValidate checks exactly one structurally valid item; TryReadValueLength finds a complete prefix for streaming callers.

Read [the primitive-layer design](docs/design/primitive-layer.md) for consumption rules, resource accounting, and encoding policies. Structural validation checks UTF-8 and grammar; tag semantics, duplicate/equivalent keys, deterministic map ordering, and typed model behavior need additional policy. Preferred widths and forbidding indefinite encodings are separate options.

## Native AOT

Native publishing needs the platform's .NET Native AOT toolchain (including a C/C++ compiler/linker). The native executable exercises generated mutable/immutable models and collection closures as well as RFC fixtures.

```powershell
pwsh -File eng/verify-native.ps1 -RuntimeIdentifier win-x64
```

On Linux pass linux-x64. The script isolates RID-specific restore locks under artifacts/aot-locks so native publishing does not rewrite committed dependency locks. CI uses the same script on Windows and Linux; local Windows verification is recorded in docs/research.md.

Read [the research](docs/research.md) for reference revisions and architecture decisions, and [the roadmap](docs/roadmap.md) for measurable completion criteria. The experimental package ID Cbor is a local working choice; ownership, release license, signing identity, and publication metadata must be settled before a release.
