# Factory-only contract measurements — 2026-10-02

The baseline is 37c7d25, whose paired graphs still coexist with single-type formatter adapters, registries, inherited resolvers, and buffer fallback. The cleanup removes those APIs, emits factories directly from [CborFactory], seals the resolver, and flattens factory composition. One generic factory creation method serves every target and caller-defined buffer pair, without Type-based buffer dispatch or a cross-target factory ABI. Formatters still initialize child fields once and publish complete graphs atomically. Context methods require initialized paired formatters; they do not resolve dependencies.

`NestedModelBenchmarks` uses one or 64 orders with eight lines each, producing 136-byte and 8,358-byte payloads. Scalar controls use default options. Construction, round-trip checks, and runtime-asset assertions run outside measurement. This is a warmed dispatch comparison; it excludes cold initialization and resolver retention.

Sequential BenchmarkDotNet 0.15.8 runs use two launches, five warmups, eight measurement iterations per launch, and 500 ms target iterations. Host: Windows 11 x64, Intel i7-13700F, SDK 10.0.401. Modern results use the asserted net10.0 asset on .NET 10.0.12; ordinary-tier results use the asserted netstandard2.0 asset on .NET 8.0.31. Baseline reports are pair-after-modern and pair-after-compat from the preceding matched experiment. Errors are half-widths of the reported 99.9% confidence intervals. No builds, tests, or other suites run concurrently with measurement.

## .NET 10 asset

| Workload | 37c7d25 mean ± error | Cleanup mean ± error | Allocation baseline/cleanup |
| --- | ---: | ---: | ---: |
| Encode 8 lines | 213.80 ± 7.47 ns | 218.60 ± 1.41 ns | 160 B / 160 B |
| Decode 8 lines | 320.10 ± 2.27 ns | 315.60 ± 3.22 ns | 816 B / 816 B |
| Encode 512 lines | 13.20 ± 0.39 µs | 13.02 ± 0.34 µs | 8384 B / 8384 B |
| Decode 512 lines | 19.12 ± 0.30 µs | 18.50 ± 0.14 µs | 46680 B / 46680 B |
| Encode scalar | 13.37 ± 2.53 ns | 16.05 ± 6.02 ns | 32 B / 32 B |
| Decode scalar | 2.28 ± 0.03 ns | 2.11 ± 0.02 ns | 0 B / 0 B |

## netstandard2.0 asset on .NET 8

| Workload | 37c7d25 mean ± error | Cleanup mean ± error | Allocation baseline/cleanup |
| --- | ---: | ---: | ---: |
| Encode 8 lines | 431.70 ± 4.23 ns | 425.50 ± 1.54 ns | 160 B / 160 B |
| Decode 8 lines | 482.20 ± 7.59 ns | 464.20 ± 4.80 ns | 816 B / 816 B |
| Encode 512 lines | 23.74 ± 0.25 µs | 23.65 ± 0.18 µs | 8384 B / 8384 B |
| Decode 512 lines | 27.45 ± 0.21 µs | 26.76 ± 0.40 µs | 46680 B / 46680 B |
| Encode scalar | 28.79 ± 0.39 ns | 27.84 ± 0.25 ns | 32 B / 32 B |
| Decode scalar | 4.26 ± 0.02 ns | 4.07 ± 0.03 ns | 0 B / 0 B |

Interpret overlapping intervals cautiously. BenchmarkDotNet flags bimodal distributions for final modern scalar encoding and large-model encoding. The modern scalar encoding launches cluster near 10.2 ns and 21.1 ns with the same 32 B allocation; its combined mean cannot establish a speedup or rule out a regression. Investigating launch-dependent code generation needs matched disassembly and controls. These measurements check the cost of removing the compatibility layer; they do not compare against MessagePack or establish full serializer parity. Cold graphs, adverse fragmentation, and other runtime/operating-system tiers remain separate work.

Build and verify first, then run the suites separately:

```powershell
dotnet run --project benchmarks/Cbor.Benchmarks -c Release --no-build --no-restore -- --filter '*NestedModelBenchmarks*' '*RuntimePathBenchmarks.*Scalar' --job short --warmupCount 5 --iterationCount 8 --launchCount 2 --iterationTime 500 --artifacts artifacts/benchmarks/unified-final-modern
dotnet run --project benchmarks/Cbor.Benchmarks.NetStandard20 -c Release --no-build --no-restore -- --filter '*NestedModelBenchmarks*' '*RuntimePathBenchmarks.*Scalar' --job short --warmupCount 5 --iterationCount 8 --launchCount 2 --iterationTime 500 --artifacts artifacts/benchmarks/unified-final-compat
```

Local reports are retained under the named ignored artifact directories. Fixtures, asset assertions, and this record are versioned.
