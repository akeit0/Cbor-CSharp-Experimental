# Five-library serializer comparison — 2026-10-03

Measured optimized source: [b967c6a](https://github.com/akeit0/Cbor-CSharp-Experimental/commit/b967c6a0b8ea7ac9d2e19bd36326e8c7089237e1), containing runtime implementation [5c0a160](https://github.com/akeit0/Cbor-CSharp-Experimental/commit/5c0a160172d4c9fadd0dce366363fe8d8b34fb1f). These are warmed, complete serialization/deserialization operations on equivalent CLR values. They do not establish complete serializer parity, equivalent wire schemas, or equivalent resource/security policies. The [original baseline report](serializer-comparison-baseline.md) and its raw evidence remain archived.

## Reproduction and controls

See [the comparison project](../../benchmarks/Cbor.Benchmarks.Comparison/README.md) for pinned dependencies, fixture definitions, policy differences, and commands. MessagePack v4 uses the unmodified submodule at a9057be29b0621d56a4312f79172e759bbb4e1de, including its original runtime and source generator. Other providers use locked NuGet packages: CAPCOM.REDox.Cbor 1.0.0, PeterO.Cbor 4.5.5, and System.Formats.Cbor 10.0.12. One executable loads all five providers; PeterO uses an extern alias, and our runtime assembly is Cbor.CSharp.

BenchmarkDotNet 0.15.8 runs 40 cases sequentially, with two worker launches, four warmup iterations and six measured iterations per launch, targeting 200 ms iterations. This refresh follows broad correctness verification of the optimized implementation. The shorter settings differ from the original baseline's 5/8/500 ms settings; use the contemporaneous within-run provider comparisons, and the separate matched [before/after confirmation](hot-paths.md) for optimization effects. Builds and tests finish before timing; no other suites or builds run during measurement. Host: Windows 11 x64 (10.0.26200.9457), Intel Core i7-13700F, 16 physical / 24 logical cores. Compiler SDK: 11.0.100-rc.1.26425.128. Runtime: .NET 10.0.12, x64 RyuJIT x86-64-v3, concurrent workstation GC. BenchmarkDotNet selects its high-performance power plan. The comparison compiler uses the SDK required by upstream v4; workers execute the asserted .NET 10 runtime assets. PeterO supplies a .NET Standard 1.0 asset. Every worker checks its loaded assets and generated MessagePack model formatter.

The initial 40-case run produced minimum-iteration warnings in several decoders. A targeted follow-up selects all five string-map decoders and PeterO model decoding at both sizes, using two launches, five warmups, eight measurements and 500 ms iteration targets. Those seven longer results replace the corresponding cells in the tables; all other cells come from the initial refresh. Both runs and all warnings remain in the retained evidence. Time ratios below divide the selected provider mean by the selected Cbor mean; original CSVs also retain BenchmarkDotNet's within-job ratio distributions.

Every encode returns a new owned byte array. Every decode materializes the full CLR array, dictionary, or nested model. Setup warms all providers and validates every field/value; CBOR collections are cross-read by every CBOR provider. System.Formats.Cbor independently reads all CBOR model encodings. Its schema-specific adapters produce exactly the same bytes as Cbor on these fixtures. PeterO DOM construction and CLR conversion are included in timed APIs; REDox includes document parsing and typed conversion when decoding and its native writer path when encoding. No prebuilt document is timed. Cold resolver/converter initialization is excluded.

Errors in the tables are BenchmarkDotNet's 99.9% confidence-interval half-widths. Ratios use Cbor as the baseline within each operation and fixture. Allocations are managed bytes per completed operation, not retained heap size or native allocation. Raw measurements retain both launches, pre-filter WorkloadActual samples, overhead measurements, and the WorkloadResult samples used for the summaries.

## Payload sizes

| Fixture | Cbor | MessagePack v4 | REDox | System.Formats adapter | PeterO |
| --- | ---: | ---: | ---: | ---: | ---: |
| 1,024 signed integers | 3,005 B | 2,879 B | 3,005 B | 3,005 B | 3,005 B |
| 1,024 UTF-8 string keys | 14,997 B | 15,005 B | 16,011 B | 14,997 B | 14,997 B |
| 1 order / 8 lines | 144 B | 115 B | 301 B | 144 B | 300 B |
| 64 orders / 512 lines | 8,870 B | 7,117 B | 18,099 B | 8,870 B | 18,035 B |

Cbor and the System.Formats adapter use integer-keyed CBOR model maps; MessagePack uses indexed arrays; REDox uses property-name CBOR maps; PeterO uses camel-cased property-name CBOR maps. Native schemas are intentionally retained. Payload sizes expose this difference; dividing timings by bytes does not make the operations equivalent.

## Measured operations

All times below are microseconds, including the small model. Time ratios are provider/Cbor; lower is faster. Allocation columns show managed bytes per operation from the GC measurements. Original summary CSVs retain standard deviations, ratio deviations, and GC rates.

### 1,024 signed integers

| Provider | Encode µs ± error | Time/Cbor | Encode alloc B | Decode µs ± error | Time/Cbor | Decode alloc B |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Cbor | 1.9710 ± 0.0283 | 1.00 | 3,032 | 3.4440 ± 0.1222 | 1.00 | 4,120 |
| MessagePack v4 | 1.2750 ± 0.0231 | 0.65 | 2,904 | 2.7330 ± 0.0299 | 0.79 | 4,120 |
| REDox | 2.2800 ± 0.0659 | 1.16 | 3,032 | 6.5100 ± 0.0747 | 1.89 | 4,120 |
| System.Formats adapter | 4.8650 ± 0.0917 | 2.47 | 10,680 | 7.9690 ± 0.0599 | 2.31 | 4,536 |
| PeterO | 24.1830 ± 0.4321 | 12.27 | 174,936 | 32.0530 ± 0.3926 | 9.31 | 221,064 |

### 1,024 UTF-8 string keys

| Provider | Encode µs ± error | Time/Cbor | Encode alloc B | Decode µs ± error | Time/Cbor | Decode alloc B |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Cbor | 14.5640 ± 0.2024 | 1.00 | 15,024 | 38.3600 ± 0.4190 | 1.00 | 72,168 |
| MessagePack v4 | 7.8940 ± 0.0542 | 0.54 | 15,032 | 26.3000 ± 0.2830 | 0.69 | 72,168 |
| REDox | 14.4280 ± 0.0988 | 0.99 | 16,096 | 39.0600 ± 0.2230 | 1.02 | 72,168 |
| System.Formats adapter | 37.4800 ± 0.5781 | 2.57 | 120,560 | 61.4000 ± 3.3700 | 1.60 | 145,848 |
| PeterO | 863.2030 ± 24.1346 | 59.27 | 390,040 | 494.4400 ± 11.0090 | 12.89 | 608,880 |

### 1 order / 8 lines

| Provider | Encode µs ± error | Time/Cbor | Encode alloc B | Decode µs ± error | Time/Cbor | Decode alloc B |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Cbor | 0.2248 ± 0.0066 | 1.00 | 168 | 0.3189 ± 0.0043 | 1.00 | 888 |
| MessagePack v4 | 0.0873 ± 0.0023 | 0.39 | 144 | 0.2213 ± 0.0069 | 0.69 | 848 |
| REDox | 0.3463 ± 0.0065 | 1.54 | 408 | 0.9400 ± 0.0077 | 2.95 | 888 |
| System.Formats adapter | 0.9273 ± 0.0228 | 4.13 | 2,736 | 1.3881 ± 0.0138 | 4.35 | 2,416 |
| PeterO | 3.5290 ± 0.0417 | 15.70 | 14,192 | 10.7000 ± 0.1000 | 33.56 | 22,176 |

### 64 orders / 512 lines

| Provider | Encode µs ± error | Time/Cbor | Encode alloc B | Decode µs ± error | Time/Cbor | Decode alloc B |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Cbor | 13.3664 ± 0.1816 | 1.00 | 8,896 | 19.2680 ± 0.4349 | 1.00 | 49,272 |
| MessagePack v4 | 4.9906 ± 0.4110 | 0.37 | 7,144 | 13.5466 ± 0.3090 | 0.70 | 47,192 |
| REDox | 18.5879 ± 0.3220 | 1.39 | 20,728 | 55.5709 ± 1.1210 | 2.88 | 49,272 |
| System.Formats adapter | 40.4009 ± 0.7705 | 3.02 | 42,280 | 68.1861 ± 0.8117 | 3.54 | 50,800 |
| PeterO | 225.2274 ± 2.5981 | 16.85 | 854,544 | 693.3300 ± 5.8030 | 35.98 | 1,290,448 |

## Interpretation and remaining work

MessagePack v4 remains faster than Cbor in all eight operation/fixture combinations. For integer arrays, Cbor/MessagePack mean ratios are now approximately 1.26 for decoding and 1.55 for encoding. Across these fixtures, mean ratios range from about 1.26 to 1.46 for decoding and 1.55 to 2.68 for encoding. Integer-array and string-map decoding allocations match; MessagePack's model decoding allocates 40 B less for one order and 2,080 B less for 64 orders. Both use generated formatters with warmed dependencies. These measurements do not attribute the remaining gaps to a specific dispatch, encoding, policy or schema difference.

Cbor now encodes the integer array faster than REDox (1.971 versus 2.280 µs), with separated intervals, and its other integer/model operations remain faster than REDox. Map encoding intervals overlap. The longer map-decode confirmation has a slightly lower Cbor mean than REDox (38.360 versus 39.060 µs), with narrowly separated intervals; the initial shorter run's intervals overlap. Treat this small difference as a result on this host and fixture, rather than a broad UTF-8 superiority claim. Cbor is faster than the System.Formats adapters and PeterO in all measured combinations.

The [matched before/after confirmation](hot-paths.md) establishes the integer-array optimization gains; the archived original five-library baseline used longer iteration settings, so subtracting the two main reports is not a controlled optimization experiment. Payload sizes and Cbor's managed allocation counts remain unchanged.

Large-model Cbor encoding in this refresh measures 13.366 µs, with retained launch means 13.438 µs (6 samples) and 13.280 µs (5 samples), at 8,896 B per operation. The preceding matched confirmation measured 13.819 µs against a 13.210 µs baseline and raised a regression concern. This newer observation does not determine its cause or erase the prior evidence; launch-dependent scalar/model encoding still needs matched disassembly/runtime controls. This suite has no scalar control.

The initial short refresh emitted minimum-iteration warnings for PeterO model decoding and Cbor/REDox/PeterO map decoding. The report uses longer targeted confirmation for all map decoders and both PeterO model sizes, preserving the initial evidence separately. That confirmation has no minimum-iteration warnings. PeterO map decode's initial 759.704 ± 262.999 µs estimate becomes 494.440 ± 11.009 µs in the longer run. System.Formats map decode is flagged as bimodal (mValue 3.75), with retained launch means 58.259 µs (7 samples) and 64.152 µs (8 samples). Its combined mean is not one stable performance regime; the cause is unestablished. No additional full-suite rerun was used to select favorable results.

Open performance coverage includes cold initialization/retention, malformed/adverse input, floating-point/tagged built-ins, .NET Standard and .NET 8/9 tiers, segmented buffers, Native AOT performance, other CPUs, and Linux. No hosted CI was run. The measured implementation already passed 2,406 tests across all five runtime assets, package consumers on .NET 8/9/10, and project/package Native AOT execution on Windows. Only documentation and evidence exports changed during this refresh.

## Retained evidence

[Summary CSVs, allocation measurements, both launches and targeted decode confirmation](serializer-comparison-data/README.md) are versioned without absolute machine paths. All 40 initial cases and seven follow-up cases succeeded: 94 worker processes, 592 raw workload samples before filtering, and 94 GC records. The initial run took approximately 5 minutes 19 seconds; targeted confirmation took 2 minutes 33 seconds. Full local reports/logs remain in ignored artifacts directories. The [original baseline](serializer-comparison-baseline.md) and its complete data remain archived. The measured optimized source predates this documentation-only refresh commit; runtime implementation is unchanged.
