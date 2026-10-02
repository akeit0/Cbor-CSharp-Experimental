# Runtime path measurements — 2026-10-02

These historical measurements precede operation caching and buffer-pair formatter fields. See [the current matched comparison](operation-resolution.md) for the retained typed-model architecture.

`RuntimePathBenchmarks` measures eight repeated integer object members, 1,024-entry integral/string maps, 49,152 ASCII bytes, and 57,344 UTF-8 bytes alternating three- and four-byte code points. Segmented text uses 4,093-byte windows, including code-point seams. Setup and input construction are outside the measured methods. Object/map methods use the typed serializer and source-generated resolver; structural text checks also represent unknown-member traversal costs.

| Workload | Before mean | Revised mean | Managed allocation per operation |
| --- | ---: | ---: | ---: |
| Encode repeated members | 105.70 ns | 73.81 ns | 48 B |
| Decode repeated members | 104.50 ns | 71.85 ns | 48 B |
| Decode integer map | 47.22 µs | 33.28 µs | 22,192 B |
| Decode string map | 56.32 µs | 44.25 µs | 63,704 B |
| Scan ASCII text | 14.85 µs | 0.31 µs | 0 B |
| Scan Unicode text | 35.36 µs | 14.65 µs | 0 B |
| Scan segmented Unicode text | 42.10 µs | 14.89 µs | 0 B |

Allocations are unchanged. Before measurements used the preceding implementation: generated per-member resolution, ContainsKey/Add dictionary insertion, and scalar UTF-8 state for every byte. Revised measurements use per-object typed formatter locals, modern entry-reference insertion, runtime ASCII scanning, and whole-code-point UTF-8 checks.

These are BenchmarkDotNet 0.15.8 ShortRun measurements (one launch, three warmups, three measurement iterations) on Windows x64, Intel i7-13700F, .NET 10.0.12, SDK 10.0.401. Confidence intervals are wide with three samples: revised object encoding reports ±20.24 ns and string-map decoding ±8.69 µs at 99.9% confidence. Treat the figures as diagnostic evidence for these workloads, not portable throughput guarantees or MessagePack parity. This run does not measure compatibility assets, large nested graphs, or adversarial segmentation.

An intermediate implementation using `Utf8.IsValid` for complete prefixes regressed this mixed Unicode workload to 58.77 µs. It was replaced after measurement. [Ascii.IsValid](https://learn.microsoft.com/en-us/dotnet/api/system.text.ascii.isvalid?view=net-10.0) provides the modern ASCII check. Dictionary entry references follow the storage-stability requirement of [CollectionsMarshal.GetValueRefOrAddDefault](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.collectionsmarshal.getvaluereforadddefault?view=net-10.0).

Local full reports are retained under `artifacts/benchmarks/runtime-before`, `runtime-after`, and `runtime-final`; artifacts are ignored by Git. The fixtures and this measurement record are versioned. Reproduce current measurements with:

```powershell
dotnet run --project benchmarks/Cbor.Benchmarks -c Release -- --filter '*RuntimePathBenchmarks*' --job short --artifacts artifacts/benchmarks/runtime-paths
```
