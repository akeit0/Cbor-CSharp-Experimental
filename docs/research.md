# Reference research and scaffold decisions

Research date: 2026-10-02. These observations come from shallow checkouts of the requested branch and repository, including project files, runtime sources, analyzer sources, test projects, and workflows. No MessagePack runtime implementation or signing key was copied. The context ownership analyzer adapts Foundation's MIT-licensed analyzer, with attribution and the full license in THIRD-PARTY-NOTICES.txt.

## Reproducible sources

| Source | Inspected commit | Key files |
| --- | --- | --- |
| [MessagePack-CSharp v4](https://github.com/MessagePack-CSharp/MessagePack-CSharp/tree/a9057be29b0621d56a4312f79172e759bbb4e1de) | a9057be29b0621d56a4312f79172e759bbb4e1de | MessagePack.slnx; src/MessagePack/MessagePack.csproj; SourceGenerator and CodeFixes projects; Annotations project; net8/net9, robustness, fuzz, and NativeAot test projects |
| [SerializerFoundation](https://github.com/Cysharp/SerializerFoundation/tree/f1acf0b62f8eb0176649be8b75a83e1c8c1d2d73) | f1acf0b62f8eb0176649be8b75a83e1c8c1d2d73 | SerializerFoundation.slnx; core/analyzer project files; IReadBuffer.cs; IWriteBuffer.cs; buffer implementations; analyzer release tracking; README |
| [RFC 8949](https://www.rfc-editor.org/rfc/rfc8949.html) | Published December 2020 | Wire format, deterministic encoding, validity, security, Appendix A vectors |
| [System.Formats.Cbor source](https://github.com/dotnet/runtime/tree/v10.0.0/src/libraries/System.Formats.Cbor) | .NET 10 source reference | Independent reader/writer oracle; package pinned to servicing version 10.0.12 |

MessagePack's v4 README is only a heading at the inspected revision, so the project/source files are the substantive architectural evidence. v4 is a moving rewrite branch; these findings are not a claim about a released v4 product. SerializerFoundation 1.0.0 was verified as available from NuGet's version index and used as the dependency.

## What carries across

MessagePack's runtime directly references SerializerFoundation 1.0.0 and packages its own generator and code-fix assemblies under analyzers/dotnet/cs. The core runtime has .NET Standard compatibility assets and modern .NET assets. The serializer operates through generic buffer types, avoiding an interface-typed hot path on runtimes that allow ref structs as generic arguments.

The CBOR runtime references Foundation directly, preserving its analyzer flow. Helpers take buffers by ref, and the entry-point owner disposes them in finally. On .NET 9+ the generic constraints include allows ref struct; compatibility builds omit that constraint and select Compatible buffers. Windows tests execute both .NET Standard assets through .NET 8 and the modern assets through .NET 9/10. Actual .NET Framework execution has not been verified.

Foundation's contracts require implementations to be structs (SF001), prevent copying single-owner buffers (SF002), and require overrides for marked modern methods (SF003). The package supplies these analyzers. The scaffold does not recreate Foundation or its pool/sequence machinery.

The incremental source generator targets netstandard2.0 with a Roslyn 4.3.1 API reference. Compiler-driver fixtures use the pinned SDK’s compiler and symbols matching their net10.0 runtime reference; installed .NET 8/9/10 consumers exercise target-specific generation. Explicit roots produce closed AOT-safe object/enum/collection formatter graphs and contract diagnostics. Compiler-driver tests and real generated consumers verify its behavior. Code fixes remain a separate, currently empty project because Workspaces dependencies belong to IDE hosts.

The current formatter contract follows the inspected v4 runtime’s [paired interface](https://github.com/MessagePack-CSharp/MessagePack-CSharp/blob/a9057be29b0621d56a4312f79172e759bbb4e1de/src/MessagePack/IMessagePackFormatter.cs), [factory](https://github.com/MessagePack-CSharp/MessagePack-CSharp/blob/a9057be29b0621d56a4312f79172e759bbb4e1de/src/MessagePack/MessagePackFormatterFactory.cs), and [sealed resolver](https://github.com/MessagePack-CSharp/MessagePack-CSharp/blob/a9057be29b0621d56a4312f79172e759bbb4e1de/src/MessagePack/MessagePackFormatterResolver.cs). Each formatter closes over its write/read buffers and resolves child fields once in Initialize. Graphs handle recursive dependencies and publish atomically. Generated [CborFactory] roots are factories directly, options compose factory chains, and the sealed resolver only caches completed paired graphs. The operation-local pool and single-type compatibility contracts have been removed. Factory creation is generic on every target, and caller-defined buffers are tested across all five assets. Type-based buffer dispatch and cross-target binary adapters are absent. Buffer tiers remain because the supported target frameworks expose different Foundation APIs. CBOR value-returning deserialization and resource policies remain independently specified choices.

The inspected v4 runtime contains its attributes. MessagePack.Annotations is a compatibility shim for previously compiled v3 consumers, not a model to repeat in a new library. CBOR attributes therefore live in Cbor. There is no v3 assembly identity to preserve and no upstream signing key to reuse.

## What must be designed for CBOR

A mechanical rename cannot produce CBOR compatibility. RFC 8949 specifies eight major types, negative integers as -1 minus the encoded argument, definite/indefinite containers and strings, semantic tags, simple values, and multiple float widths. The implementation includes primitive APIs and typed serialization over these forms. Broad tagged CLR mappings remain unfinished.

Writer policy and reader policy are separate. Preferred numeric widths, strict UTF-8, null/undefined, canonical NaN, and negative zero now have implementations and fixtures. Default reading accepts wider well-formed numeric encodings. Core deterministic encoding still needs additional map-order and representation rules; length-first ordering is distinct. Duplicate/equivalent keys and registered-tag semantics remain typed-layer work.

The object model now uses explicit integer map keys, required/unknown-member rules, constructor binding, and generated factory roots. These contracts are described in docs/design/typed-serializer.md. Tagged CLR representations still need explicit mappings. MessagePack extension types, LZ4 framing, and SignalR protocol identities cannot be adopted as CBOR wire contracts.

## Project and tooling decisions

| Decision | Reason |
| --- | --- |
| XML Cbor.slnx with src/tests/benchmarks/sandbox folders | Matches the reference solution organization while exposing a smaller initial scope |
| Stable .NET 10 SDK and explicit C# 14 for the core | Foundation exposes C# 14 extension members. The isolated v4 comparison uses upstream's .NET 11 preview compiler while measuring .NET 10 assets; analysis rules remain pinned to .NET 10 |
| Runtime netstandard2.0/2.1 and net8.0/net9.0/net10.0 | Preserves Compatible buffers below .NET 9 while using supported .NET 8 dictionary entry references; .NET 11 is deferred until a concrete API or test requires it |
| One net8/net9/net10 test project plus forced netstandard2.0/2.1 consumers | Checks all five assets with shared test source |
| Separate conformance and robustness suites | Keeps byte interoperability and malformed-input behavior independently visible |
| Actual native publish/run smoke | A successful managed run or IsAotCompatible declaration is insufficient evidence |
| Central versions, lock files, warnings as errors, package validation | Makes dependency changes reviewable and all shipped assets build consistently |
| Explicit runtime assembly name Cbor.CSharp; namespace and package ID Cbor | Allows PeterO.Cbor's CBOR assembly to load in the same process; a compile-time extern alias alone cannot resolve a runtime identity collision |
| One runtime package bundles analyzer DLLs | Establishes consumer installation layout without publishing empty generator packages |
| No compression, Unity, ASP.NET, SignalR packages yet | Add integrations once CBOR core and protocol contracts are usable |

NuGet packaging is validated locally, including the absence of Roslyn/Workspaces runtime dependencies. Cbor is a provisional package name; no feed publication or ownership assertion is made. Original code uses the user-selected Unlicense; the adapted Foundation analyzer retains its MIT license and attribution. The package declares Unlicense AND MIT and includes both notices. Author metadata and a strong-name policy remain release decisions.

## Quality evidence and limits

Initial Windows verification: Release solution build with zero warnings/errors; 59 passing test cases across five target/suite executions; native win-x64 publish and execution; sandbox execution; benchmark discovery; experimental NuGet pack and contents checks. The checks cover the integer prototype and infrastructure only. CI workflow definitions provide Windows/Linux build/test/package and native smoke jobs, but hosted CI was not run in this workspace.

Continuation evidence includes all five runtime assets executed, all 81 Appendix A encodings and Appendix F malformed inputs, exhaustive half patterns, seeded numeric/oracle checks, generated nested items/mutations, strict UTF-8 across seams, explicit resource bounds, installed-package consumers, and actual SF002 enforcement. Integer ShortRun microbenchmarks have been executed; they do not establish object-serializer parity. See the primitive-layer design for exact behavior.

Typed continuation verification includes generated mutable/immutable contracts, records, structs, enums, recursive graphs, nested collections, shared unknown-member budgets, duplicate known/dictionary keys, and process-keyed dictionary hashing. SipHash-2-4 is checked against all 64 vectors from the [authors' CC0 reference](https://github.com/veorq/SipHash); the implementation is internal and the hash key is never serialized. Independent typed interop checks 1,000 nested object values against System.Formats.Cbor. Generator diagnostic and edited-contract tests have a dedicated project. Actual Windows Native AOT executes generated models and keyed dictionaries.

Latest local typed verification: eng/verify.ps1 passed with 2,241 tests, zero build warnings/errors, locked restore, whitespace verification, package inspection, freshly installed generated consumers on .NET 8/9/10, and actual SF002/CBOR003 rejection, including collection expressions on the current compiler host. eng/verify-native.ps1 -RuntimeIdentifier win-x64 published and executed both the project corpus and a fresh packaged Native AOT consumer. Both paths execute recursive generated models, keyed dictionaries, and typed wire values through the paired factory contract. New coverage includes exhaustive Half round trips, full-range CborInteger, all valid simple values, arbitrary tag chains, and independent integer/bignum interoperability. Initialization tests cover concurrency, cycles, atomic publication, and retry after failures. The chunk traversal returns typed string/byte-array results without object casts.

Matched measurements against bce6094 show lower warmed nested-model times with unchanged allocations after adopting paired formatters and initialized child fields. Benchmark workers now assert the loaded runtime asset; earlier mislabeled compatibility timings and derived speedup claims are withdrawn. Corrected map measurements compare asserted netstandard2.0 and net8.0 assets on the same host. See [buffer-pair measurements](benchmarks/operation-resolution.md) for controls, confidence intervals, allocation evidence, and reproduction, and [wire-value measurements](benchmarks/wire-values.md) for bignum buffer shapes and API-related allocations. These results do not measure or establish MessagePack parity.

Registered-tag built-ins, full deterministic/key-equivalence profiles, unions/reference preservation, IDE fixes, API release baselines, older-engine hosts, integrations, and coverage-guided fuzzing remain unfinished. Hosted CI was not run locally. The roadmap lists remaining work toward comparable serializer quality.

The experimental GitHub repository is [akeit0/Cbor-CSharp-Experimental](https://github.com/akeit0/Cbor-CSharp-Experimental). Hosted verification remains prepared only: the workflow has a manual dispatch trigger, repository Actions are disabled, and no hosted run has been requested.

The [five-library comparison](../benchmarks/Cbor.Benchmarks.Comparison/README.md) uses the pinned, unmodified MessagePack v4 submodule and locked NuGet packages for CAPCOM.REDox.Cbor 1.0.0, PeterO.Cbor 4.5.5, and System.Formats.Cbor 10.0.12. One executable uses an extern alias for PeterO and loads the explicit Cbor.CSharp assembly. Local fixture checks assert loaded runtime assets, generated MessagePack model formatters, every decoded field, CBOR collection cross-reads, and independent model decoding. The assembly rename passed the complete 2,241-test verification and both Windows Native AOT paths.

The [original measured comparison](benchmarks/serializer-comparison-baseline.md) completed all 40 cases on .NET 10.0.12 with the upstream-required compiler. MessagePack v4 was faster on these warmed workloads; Cbor did not establish performance parity. REDox also led on integer-array encoding and string-map decoding. Large-model Cbor encoding was bimodal. Its original summary CSVs and both launches remain archived; runtime/buffer tiers, additional built-ins, cold paths, adverse input, and other hosts remain open coverage.

The subsequent [focused optimization](benchmarks/hot-paths.md) applies MessagePack v4's struct element codecs and buffer batching to built-in integer arrays, keeps complete valid CBOR prefixes on direct byte/length checks, and simplifies definite-list loops. Custom scalar overrides and resource-budget failure behavior remain intact. Short development runs took approximately 30 seconds; the verified candidate then received two-launch confirmation across 14 Cbor cases/controls. Integer-array decode/encode means improved by about 41%/26% with unchanged allocations, while large-model encoding's 4.6% higher mean remains an open concern. These measurements do not refresh the cross-library comparison.

The final implementation passes 2,406 tests across all five runtime assets, fresh packaged .NET 8/9/10 consumers and analyzer enforcement, plus both project and fresh packaged Windows Native AOT paths. New checks cover all integer array types, exact guarded output windows, resource-budget failure prefixes, complete/fragmented/malformed CBOR prefixes, preferred-width rejection and native generated array roots. Hosted CI remains disabled.

The [main comparison refresh](benchmarks/serializer-comparison.md) measures all 40 five-library cases against that verified implementation with shorter 200 ms iterations and two launches. Seven decode cases then receive longer 500 ms confirmation after initial minimum-iteration warnings; all initial/follow-up data are retained. MessagePack v4 still leads all eight operations, with Cbor/MessagePack integer-array mean ratios of about 1.26 for decode and 1.55 for encode. Cbor now beats REDox on integer-array encoding. Map differences, System.Formats map-decode bimodality and launch-dependent Cbor encoding remain documented limits. Runtime code and dependencies did not change during the refresh.
