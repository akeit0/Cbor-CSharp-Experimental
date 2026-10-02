# Quality roadmap

Aim for a complete serializer and the reference project's depth of evidence. Project names, validation, and a few round trips do not establish parity. The current implementation includes typed serialization and generated object contracts; the following milestones retain their full completion criteria.

| Milestone | Work | Completion evidence |
| --- | --- | --- |
| 0. Project foundation | Solution, buffer dependency, package boundaries, runtime asset tests, integer smoke, CI | Release build/test/pack; real AOT smoke; package contents checked |
| 1. Complete primitive layer | All major types; signed/unsigned ranges; half/single/double; UTF-8; tags; null/undefined; definite/indefinite forms; skip/scanning | RFC Appendix A fixtures; malformed Appendix F inputs; independent oracle comparisons; each sequence seam; all truncation prefixes; explicit consumed-byte behavior |
| 2. Safe reader/writer state | Nesting, element/byte budgets, checked length arithmetic, cancellation at appropriate boundaries; validity and duplicate-key policy | Adversarial nesting/length tests; bounded resource use; preserved failure semantics; multi-item streaming tests |
| 3. Typed serialization | Formatter/factory/resolver contracts; primitive/collection/built-in mappings; immutable per-call options; Span/sequence/writer entry points | Full known-answer formatters; independent interop; concurrent resolver tests; explicit wire mapping documentation |
| 4. Source generation | Runtime attributes; incremental generation; diagnostics/release tracking; separate IDE fixes; explicit AOT registration | Real Roslyn compilation/diagnostic/code-fix tests; partial/nested/generic/nullable/required models; unsupported models diagnosed; native generated-object tests |
| 5. Encoding profiles | Preferred encoding; core deterministic encoding; explicitly separate length-first ordering; tag/float/key-equivalence decisions | Byte-exact deterministic vectors including map order, NaNs and negative zero; accepted/rejected profile cases; cross-implementation fixtures |
| 6. Robustness and performance | Shrinking property tests; coverage-guided fuzz target/harness separation; retained crash corpus; realistic benchmarks/disassembly | Reproducible seeds/corpus; bounded rejection behavior; allocation/throughput and payload-size reports across buffer tiers; comparable workload settings |
| 7. Ecosystem and release | Targeted integration packages; Unity/older-host compiler tests where promised; packaging/consumer tests; API baseline; signing/license/version policy | Install from packed NuGet into independent consumers; generator and Foundation analyzer flow; generated AOT models; platform CI; release metadata; stable baseline |

## Implementation boundaries

The experimental public API and model attributes live in Cbor. ICborFormatter<TWriteBuffer, TReadBuffer, T>, factory composition, initialized resolver graphs, immutable options, per-operation contexts, serializer entry points, and explicit generated factory roots are implemented. No release API baseline is frozen yet. The unsigned-integer prototype has been removed; samples and installed consumers use generated models.

Current evidence includes RFC Appendix A/F fixtures, exhaustive half decoding, execution of all five runtime assets, bounded iterative traversal, strict UTF-8, independent generated-value/mutation comparisons, native publishing, and installed-package analyzer enforcement. See docs/design/primitive-layer.md for semantic boundaries.

Typed/generated evidence includes mutable/immutable models, constructor binding, records, structs, enums, nullable values, arrays/lists/dictionaries, recursive graphs, required/duplicate/unknown keys, shared budgets, chunked strings, process-keyed hashing, and independent nested-object interop. The dedicated generator suite exercises unsupported contracts and input edits. Native and installed-package consumers compile and execute generated contracts.

Closed generic models and explicitly opted-in inheritance use substituted compile-time contracts, inherited key/override checks, constructor binding, bounded iterative dependency traversal, and independent object interop. The native verification script builds a fresh package and executes an independent packaged Native AOT consumer in addition to the project-based corpus.

Continue with broader tagged CLR built-ins, union models, external formatter annotations, deterministic maps/key equivalence, and realistic object performance comparisons. Cancellation-aware outer streaming, coverage-guided fuzzing/shrinking, and IDE fixes remain unfinished. Preserve single-pass typed decoding and shared resource accounting as these features grow.

Typed wire coverage now includes complete major-type integer values, arbitrary simple values, tag chains, BigInteger, and Half on NET8+ assets. Buffer-pair factory composition initializes child fields once, supports recursive graphs, and publishes complete graphs atomically. Concurrency, failed initialization, override precedence, and rejection of incorrectly typed factory results have focused tests. Standard integer/bignum encodings and simple values match an independent oracle; exhaustive Half round trips, malformed/truncated input, segmented reads, and shared limits are covered. Additional date/time, decimal, and UUID mappings still need explicit interoperable contracts.

Use Foundation's buffer implementations rather than maintaining duplicate buffer/pool code. On .NET Standard, verify both compatibility assets; on modern runtimes, keep .NET 9 as the first ref-struct generic tier. Add true .NET Framework or engine consumers only if those become supported targets.

Keep System.Formats.Cbor in tests/benchmarks as an independent reference. Tests must account for conformance-mode differences; it does not define all application-specific tag or .NET type mappings. Maintain independently specified byte fixtures as well.

## Packages to add only when needed

Consider Cbor.AspNetCoreMvcFormatter for application/cbor content negotiation, and a separate CBOR hub protocol only after its envelope, protocol name, negotiation, and interoperability are specified. Unity/Godot adapters need actual supported-host validation. Compression can be a message processing layer, but must not masquerade as standard CBOR data.

Cbor.Tests.SourceGenerator now exercises the real generator. Add code-fix compiler tests with the first IDE fix. Add fuzz target and harness projects with an instrumented/uninstrumented split; the present random-input suite is not coverage-guided fuzzing.

## Release gate

Before publishing, settle the package ID and repository/author metadata, choose a signing policy, commit an API compatibility baseline, and verify AOT consumers from packaged artifacts across platforms. Original code uses the Unlicense; the bundled analyzer adaptation retains MIT attribution and package license metadata records both. Generated installed-package consumers already run on .NET 8/9/10. Current tests do not establish MessagePack v4 performance, complete semantic conformance, or release readiness.
