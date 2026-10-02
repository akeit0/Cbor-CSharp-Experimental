# Operation formatter resolution — 2026-10-02

`NestedModelBenchmarks` serializes and deserializes an order batch containing one or 64 orders, each with eight line objects. Every order has an identifier, customer string, and list of lines; every line has a SKU, quantity, and price. The graph therefore exercises three model types, two list types, and repeated scalar/string dependencies. The small batch encodes to 136 bytes and the large batch to 8,358 bytes. Construction, serialization of decode fixtures, and shape checks run in setup outside measurement.

The retained implementation resolves each used type once through operation context storage. Generated typed locals still avoid repeated lookups within a single object's member loop. Two inline entries avoid a rental for flat models; wider graphs use a pooled table that grows at half occupancy and clears returned arrays. Exact type keys make reference reinterpretation safe. Custom overrides remain scoped to the supplied options and the first completed resolution in that operation. No formatter dependency graph is initialized globally.

## Measurements

Final measurements compare commit `1b11f35` with the retained implementation. A separate baseline checkout uses the same benchmark fixtures. Runs are sequential, with no concurrent builds or tests. BenchmarkDotNet 0.15.8 uses two launches, five warmups, and eight measurement iterations per launch on Windows x64, Intel i7-13700F, SDK 10.0.401. Modern runs use .NET 10.0.12 with the net10.0 asset; compatibility runs use .NET 8.0.31 and explicitly reference the netstandard2.0 asset. This is evidence about those assets on these JIT hosts, not a measurement of .NET Framework, Mono, other platforms, or MessagePack parity.

### .NET 10 asset

| Workload | Before mean ± error | Retained mean ± error | Allocation before/after |
| --- | ---: | ---: | ---: |
| Encode 8 lines | 527.20 ± 15.85 ns | 457.10 ± 5.93 ns | 160 B / 160 B |
| Decode 8 lines | 584.90 ± 10.12 ns | 592.70 ± 8.80 ns | 816 B / 816 B |
| Encode 512 lines | 29.21 ± 1.02 µs | 21.24 ± 0.22 µs | 8384 B / 8384 B |
| Decode 512 lines | 33.93 ± 0.91 µs | 27.75 ± 0.15 µs | 46680 B / 46680 B |
| Encode eight integer fields | 73.40 ± 0.98 ns | 76.86 ± 0.54 ns | 48 B / 48 B |
| Decode eight integer fields | 76.36 ± 1.47 ns | 74.27 ± 0.84 ns | 48 B / 48 B |
| Encode default integer scalar | 14.00 ± 0.11 ns | 14.78 ± 0.09 ns | 32 B / 32 B |
| Decode default integer scalar | 6.79 ± 0.02 ns | 7.94 ± 0.12 ns | 0 B / 0 B |

### netstandard2.0 asset on .NET 8

| Workload | Before mean ± error | Retained mean ± error | Allocation before/after |
| --- | ---: | ---: | ---: |
| Encode 8 lines | 826.50 ± 13.64 ns | 785.50 ± 5.36 ns | 160 B / 160 B |
| Decode 8 lines | 913.80 ± 4.73 ns | 915.80 ± 3.90 ns | 816 B / 816 B |
| Encode 512 lines | 43.92 ± 0.71 µs | 31.07 ± 0.25 µs | 8384 B / 8384 B |
| Decode 512 lines | 51.38 ± 0.42 µs | 40.09 ± 0.50 µs | 46680 B / 46680 B |
| Encode eight integer fields | 127.30 ± 0.71 ns | 143.56 ± 4.12 ns | 48 B / 48 B |
| Decode eight integer fields | 109.30 ± 1.35 ns | 119.55 ± 1.39 ns | 48 B / 48 B |
| Encode default integer scalar | 39.38 ± 0.27 ns | 39.78 ± 1.18 ns | 32 B / 32 B |
| Decode default integer scalar | 13.37 ± 0.06 ns | 15.07 ± 0.18 ns | 0 B / 0 B |

Errors are half-widths of the reported 99.9% confidence intervals. Nested 512-line encoding improves by about 27% on the modern asset and 29% on the oldest asset; decoding improves by about 18% and 22%. The retained operation cache has a measured fixed cost on flat models: around 3.5 ns for modern encoding and 16/10 ns for oldest-asset encoding/decoding. Default scalar decoding also costs roughly 1–2 ns more. These small-message tradeoffs are retained explicitly rather than described as universal speedups; all reported allocation counts are unchanged.

Initial three-warmup ShortRun results varied substantially while tiered optimization settled. They are retained as experiment logs but are not used as the final before/after comparison. In particular, early flat-model figures suggested a much larger regression than the longer modern runs established. The final tables include flat-model and default scalar controls as well as nested workloads. Allocation measurements exclude setup and report managed bytes per operation; cold initialization or depleted/trimmed pools can still allocate. Tests exercise zero cache allocation for warmed 25-type graphs and release of custom formatter references on success and failure.

## Compatibility dictionary evaluation

.NET Standard dictionaries still use ContainsKey before decoding a value, then Add. The public APIs in these reference assemblies expose no safe entry-reference insertion. Decoding the value before TryAdd changes duplicate error priority, reader consumption, and custom formatter side effects. Reserving with Add(key, default) then assigning the decoded value still requires a second lookup. A comparer wrapper changes the supplied comparer identity; accessing private Dictionary entry layouts would add runtime-specific assumptions.

A rejected prototype reused a built-in process-keyed hash across ContainsKey/Add using temporary thread-local scopes, saving and restoring state through nested value reads. It kept custom comparer behavior and identity intact and introduced no measured managed allocation. Three-iteration ShortRun compatibility results were mixed and noisy: integer maps moved from approximately 116.5 to 104.3 µs, while short string maps moved from 134.3 to 153.5 µs. The string prototype's 99.9% interval was ±100.1 µs. This is insufficient evidence for a general speedup, and the hidden thread state adds complexity to globally shared comparers. The prototype was removed. It did not reduce dictionary probes; it only reused the expensive hash computation.

A dedicated .NET 8 runtime asset uses the public entry-reference API directly, with no comparer wrappers, thread state, reflection, or additional per-call allocation. .NET 8 can use this API while retaining Compatible buffers; ref-struct buffer support remains a separate .NET 9 capability. Forced netstandard2.0 and netstandard2.1 test projects retain legacy coverage.

### .NET 8 dictionary insertion, same .NET 8 host

| Workload | Before mean ± error | Retained mean ± error | Allocation before/after |
| --- | ---: | ---: | ---: |
| Decode 1,024 integer pairs | 55.85 ± 0.52 µs | 42.75 ± 0.35 µs | 22192 B / 22192 B |
| Decode 1,024 short-string pairs | 67.31 ± 0.37 µs | 55.47 ± 0.22 µs | 63704 B / 63704 B |

This compares the retained netstandard2.0 fallback with the dedicated .NET 8 asset on the same runtime. The new asset reduces integer-map time by about 23% and string-map time by about 18%, with identical allocations. Tests assert one hash per inserted key for .NET 8+ assets, supplied comparer identity, duplicate rejection before the value formatter, and unchanged formatter/comparer exceptions. The legacy definite-map test records six hashes for three keys; the initially unallocated indefinite-map dictionary records five.

A compatible optimization for the residual .NET Standard fallback must retain duplicate rejection before value decoding, the caller's comparer instance, original custom-comparer exception behavior, and short-key performance. The .NET Standard fallback gap remains open in the quality review.

## Reproduce

Finish builds/tests before benchmarking. Run these commands separately; do not run the suites concurrently.

```powershell
dotnet run --project benchmarks/Cbor.Benchmarks -c Release -- --filter '*NestedModelBenchmarks*' '*RuntimePathBenchmarks.*RepeatedMembers' '*RuntimePathBenchmarks.*Scalar' --job short --warmupCount 5 --iterationCount 8 --launchCount 2 --artifacts artifacts/benchmarks/operation-modern
dotnet run --project benchmarks/Cbor.Benchmarks.NetStandard20 -c Release -- --filter '*NestedModelBenchmarks*' '*RuntimePathBenchmarks.*RepeatedMembers' '*RuntimePathBenchmarks.*Scalar' '*RuntimePathBenchmarks.Decode*Map*' --job short --warmupCount 5 --iterationCount 8 --launchCount 2 --artifacts artifacts/benchmarks/operation-compat
dotnet run --project benchmarks/Cbor.Benchmarks.Net8 -c Release -- --filter '*RuntimePathBenchmarks.Decode*Map*' --job short --warmupCount 5 --iterationCount 8 --launchCount 2 --artifacts artifacts/benchmarks/operation-net8
```

Local full reports are under artifacts/benchmarks/operation-final-before-modern, operation-final-before-compat, operation-final-before-scalar-modern, operation-final-before-scalar-compat, operation-retained-after-modern, operation-retained-after-compat, and operation-net8-dictionary. Artifacts are ignored by Git; fixtures and this record are versioned.
