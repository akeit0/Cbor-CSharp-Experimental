# Buffer-pair formatter measurements — 2026-10-02

This record compares bce6094 with the paired graph implementation committed as 37c7d25, before the API cleanup. See [the cleanup measurements](clean-contract.md) for the current contract.

`NestedModelBenchmarks` uses one or 64 orders, each containing eight line objects. Three model types, two list types, and repeated scalar/string dependencies exercise realistic nested dispatch. Payloads occupy 136 and 8,358 bytes. `RuntimePathBenchmarks` supplies eight-integer-field and default-scalar controls. Construction, fixture encoding, round-trip checks, and runtime-asset assertions run outside measurement.

The baseline is commit `bce6094`, which uses generated per-object locals plus an operation-local pooled formatter cache. The revised implementation closes each formatter over its write/read buffers, acquires child fields in Initialize, and publishes completed graphs atomically. Fields and lock-free root lookup reuse selections across operations, following MessagePack-CSharp v4. Operation contexts carry budgets rather than formatter storage.

Sequential BenchmarkDotNet 0.15.8 runs use two launches, five warmups, eight measurement iterations per launch, and 500 ms target iteration time. Host: Windows 11 x64, Intel i7-13700F, SDK 10.0.401. Modern runs load the asserted net10.0 asset on .NET 10.0.12; compatibility runs load the asserted netstandard2.0 asset on .NET 8.0.31. Errors are half-widths of reported 99.9% confidence intervals. No builds, tests, or other suites ran concurrently with measurement.

## Corrected asset selection

Earlier compatibility timing tables and derived dictionary speedup percentages are withdrawn. BenchmarkDotNet's generated project reselected a newer library asset through transitive project references even when the parent benchmark forced netstandard2.0. Both baseline and revised compatibility fixtures now use an explicit assembly HintPath, a build-only project reference, and a direct Foundation dependency. Every benchmark worker checks TargetFrameworkAttribute and fails an incorrect asset selection. The following measurements replace the earlier claims.

## .NET 10 asset

| Workload | Baseline mean ± error | Revised mean ± error | Allocation baseline/revised |
| --- | ---: | ---: | ---: |
| Encode 8 lines | 456.40 ± 10.66 ns | 213.80 ± 7.47 ns | 160 B / 160 B |
| Decode 8 lines | 593.50 ± 7.60 ns | 320.10 ± 2.27 ns | 816 B / 816 B |
| Encode 512 lines | 21.59 ± 0.04 µs | 13.20 ± 0.39 µs | 8384 B / 8384 B |
| Decode 512 lines | 28.40 ± 0.40 µs | 19.12 ± 0.30 µs | 46680 B / 46680 B |
| Encode scalar | 14.91 ± 0.20 ns | 13.37 ± 2.53 ns | 32 B / 32 B |
| Decode scalar | 7.69 ± 0.37 ns | 2.28 ± 0.03 ns | 0 B / 0 B |
| Encode eight integer fields | 74.86 ± 0.46 ns | 42.52 ± 0.66 ns | 48 B / 48 B |
| Decode eight integer fields | 74.64 ± 1.50 ns | 37.31 ± 0.27 ns | 48 B / 48 B |

## netstandard2.0 asset on .NET 8

| Workload | Baseline mean ± error | Revised mean ± error | Allocation baseline/revised |
| --- | ---: | ---: | ---: |
| Encode 8 lines | 787.80 ± 12.82 ns | 431.70 ± 4.23 ns | 160 B / 160 B |
| Decode 8 lines | 932.30 ± 16.73 ns | 482.20 ± 7.59 ns | 816 B / 816 B |
| Encode 512 lines | 32.20 ± 0.37 µs | 23.74 ± 0.25 µs | 8384 B / 8384 B |
| Decode 512 lines | 40.95 ± 1.24 µs | 27.45 ± 0.21 µs | 46680 B / 46680 B |
| Encode scalar | 39.12 ± 0.77 ns | 28.79 ± 0.39 ns | 32 B / 32 B |
| Decode scalar | 14.91 ± 0.17 ns | 4.26 ± 0.02 ns | 0 B / 0 B |
| Encode eight integer fields | 136.45 ± 0.46 ns | 97.06 ± 1.20 ns | 48 B / 48 B |
| Decode eight integer fields | 122.85 ± 1.05 ns | 57.24 ± 0.49 ns | 48 B / 48 B |

The measured 512-line workload improves by approximately 39% encode / 33% decode on the modern asset and 26% encode / 33% decode on the oldest asset, with unchanged allocations. Flat models and scalar reads improve in these runs. The modern scalar-encode interval overlaps the baseline; that control does not establish a clear improvement. Warm measurements exclude graph construction, pool-cold buffer growth, and resolver lifetime costs. They do not measure MessagePack, .NET Framework, Mono, or other operating systems.

## Dictionary fallback

.NET 8+ uses CollectionsMarshal.GetValueRefOrAddDefault and rejects duplicate keys before value decoding. Tests assert one hash per valid insertion, supplied comparer identity, and unchanged formatter/comparer exceptions. The .NET Standard reference APIs provide no safe entry-reference insertion, so those assets retain ContainsKey/Add. Reserving through Add then assigning requires another lookup; decoding values before TryAdd changes duplicate error priority and side effects. Private entry layouts would depend on CLR internals.

A prior temporary thread-local hash-reuse prototype was rejected after mixed, noisy results. It introduced hidden shared-comparer state and still performed two probes. It is absent from the implementation. The supported .NET Standard fallback remains an open quality gap.

Current verified-asset runs decode 1,024-entry maps on the same .NET 8.0.31 host, with the same policies, compatible buffers, and measurement settings above. This compares two shipped library assets; it does not isolate insertion probe count from all other conditional implementation differences.

| Workload | netstandard2.0 mean ± error | net8.0 mean ± error | Allocation for both |
| --- | ---: | ---: | ---: |
| Integer keys/values | 47.26 ± 0.392 µs | 33.46 ± 0.475 µs | 21.67 KB |
| String keys/integer values | 61.01 ± 0.587 µs | 46.77 ± 0.266 µs | 62.21 KB |

Each worker asserts its loaded asset. Local reports are in artifacts/benchmarks/pair-dict-compat and pair-dict-net8.

## Reproduce

Build and test first. Run suites sequentially, using the same fixtures in a baseline checkout of bce6094; apply the assembly-reference and runtime-assertion benchmark fixes to that checkout without changing its library sources.

```powershell
dotnet run --project benchmarks/Cbor.Benchmarks -c Release -- --filter '*NestedModelBenchmarks*' '*RuntimePathBenchmarks.*RepeatedMembers' '*RuntimePathBenchmarks.*Scalar' --job short --warmupCount 5 --iterationCount 8 --launchCount 2 --iterationTime 500 --artifacts artifacts/benchmarks/pair-after-modern
dotnet run --project benchmarks/Cbor.Benchmarks.NetStandard20 -c Release -- --filter '*NestedModelBenchmarks*' '*RuntimePathBenchmarks.*RepeatedMembers' '*RuntimePathBenchmarks.*Scalar' --job short --warmupCount 5 --iterationCount 8 --launchCount 2 --iterationTime 500 --artifacts artifacts/benchmarks/pair-after-compat
dotnet run --project benchmarks/Cbor.Benchmarks.NetStandard20 -c Release -- --filter '*RuntimePathBenchmarks.Decode*Map*' --job short --warmupCount 5 --iterationCount 8 --launchCount 2 --iterationTime 500 --artifacts artifacts/benchmarks/pair-dict-compat
dotnet run --project benchmarks/Cbor.Benchmarks.Net8 -c Release -- --filter '*RuntimePathBenchmarks.Decode*Map*' --job short --warmupCount 5 --iterationCount 8 --launchCount 2 --iterationTime 500 --artifacts artifacts/benchmarks/pair-dict-net8
```

Local reports reside in artifacts/benchmarks/pair-before-modern, pair-after-modern, pair-before-compat, and pair-after-compat. Artifacts are ignored; fixtures, assertions, and this record are versioned.
