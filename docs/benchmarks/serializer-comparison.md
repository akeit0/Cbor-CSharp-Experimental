# Five-library serializer comparison — 2026-10-03

Source baseline: [f2fd74a4e263e2776129bdef3206e19eafa402a5](https://github.com/akeit0/Cbor-CSharp-Experimental/commit/f2fd74a4e263e2776129bdef3206e19eafa402a5). These are warmed, complete serialization/deserialization operations on equivalent CLR values. They do not establish complete serializer parity, equivalent wire schemas, or equivalent resource/security policies.

## Reproduction and controls

See [the comparison project](../../benchmarks/Cbor.Benchmarks.Comparison/README.md) for pinned dependencies, fixture definitions, policy differences, and commands. MessagePack v4 uses the unmodified submodule at a9057be29b0621d56a4312f79172e759bbb4e1de, including its original runtime and source generator. Other providers use locked NuGet packages: CAPCOM.REDox.Cbor 1.0.0, PeterO.Cbor 4.5.5, and System.Formats.Cbor 10.0.12. One executable loads all five providers; PeterO uses an extern alias, and our runtime assembly is Cbor.CSharp.

BenchmarkDotNet 0.15.8 runs 40 cases sequentially, with two worker launches, five warmup iterations and eight measured iterations per launch, targeting 500 ms iterations. Builds and tests finish before timing; no other suites or builds run during measurement. Host: Windows 11 x64 (10.0.26200.9457), Intel Core i7-13700F, 16 physical / 24 logical cores. Compiler SDK: 11.0.100-rc.1.26425.128. Runtime: .NET 10.0.12, x64 RyuJIT x86-64-v3, concurrent workstation GC. BenchmarkDotNet selects its high-performance power plan. The comparison compiler uses the SDK required by upstream v4; workers execute the asserted .NET 10 runtime assets. PeterO supplies a .NET Standard 1.0 asset. Every worker checks its loaded assets and generated MessagePack model formatter.

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
| Cbor | 2.7020 ± 0.0411 | 1.00 | 3,032 | 6.0440 ± 0.0626 | 1.00 | 4,120 |
| MessagePack v4 | 1.2960 ± 0.0112 | 0.48 | 2,904 | 2.7730 ± 0.0437 | 0.46 | 4,120 |
| REDox | 2.1940 ± 0.0238 | 0.81 | 3,032 | 6.6820 ± 0.1601 | 1.11 | 4,120 |
| System.Formats adapter | 5.0130 ± 0.3115 | 1.86 | 10,680 | 7.9180 ± 0.0859 | 1.31 | 4,536 |
| PeterO | 24.3940 ± 0.4419 | 9.03 | 174,936 | 32.0820 ± 0.7017 | 5.31 | 221,064 |

### 1,024 UTF-8 string keys

| Provider | Encode µs ± error | Time/Cbor | Encode alloc B | Decode µs ± error | Time/Cbor | Decode alloc B |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Cbor | 14.6370 ± 0.1776 | 1.00 | 15,024 | 41.1430 ± 0.7624 | 1.00 | 72,168 |
| MessagePack v4 | 7.9350 ± 0.0453 | 0.54 | 15,032 | 26.3500 ± 0.2092 | 0.64 | 72,168 |
| REDox | 14.8640 ± 0.2711 | 1.02 | 16,096 | 39.1230 ± 0.6515 | 0.95 | 72,168 |
| System.Formats adapter | 37.6930 ± 0.3913 | 2.58 | 120,560 | 56.8840 ± 0.3170 | 1.38 | 145,848 |
| PeterO | 872.3740 ± 5.9930 | 59.61 | 390,040 | 479.4510 ± 3.2182 | 11.66 | 608,880 |

### 1 order / 8 lines

| Provider | Encode µs ± error | Time/Cbor | Encode alloc B | Decode µs ± error | Time/Cbor | Decode alloc B |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Cbor | 0.2273 ± 0.0017 | 1.00 | 168 | 0.3450 ± 0.0118 | 1.00 | 888 |
| MessagePack v4 | 0.0874 ± 0.0012 | 0.38 | 144 | 0.2169 ± 0.0078 | 0.63 | 848 |
| REDox | 0.3380 ± 0.0033 | 1.49 | 408 | 0.9720 ± 0.0117 | 2.82 | 888 |
| System.Formats adapter | 0.9324 ± 0.0115 | 4.10 | 2,736 | 1.3741 ± 0.0188 | 3.99 | 2,416 |
| PeterO | 3.5023 ± 0.0293 | 15.41 | 14,192 | 11.2157 ± 0.2979 | 32.54 | 22,176 |

### 64 orders / 512 lines

| Provider | Encode µs ± error | Time/Cbor | Encode alloc B | Decode µs ± error | Time/Cbor | Decode alloc B |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Cbor | 13.8805 ± 0.7590 | 1.00 | 8,896 | 20.6156 ± 0.6429 | 1.00 | 49,272 |
| MessagePack v4 | 5.2941 ± 0.0519 | 0.38 | 7,144 | 13.2265 ± 0.0969 | 0.64 | 47,192 |
| REDox | 18.3812 ± 0.1425 | 1.33 | 20,728 | 55.1280 ± 0.6307 | 2.68 | 49,272 |
| System.Formats adapter | 41.4248 ± 0.9412 | 2.99 | 42,280 | 68.4747 ± 1.5983 | 3.32 | 50,800 |
| PeterO | 229.3383 ± 2.6148 | 16.56 | 854,544 | 672.4029 ± 7.4830 | 32.64 | 1,290,448 |

## Interpretation and remaining work

MessagePack v4 is faster than Cbor in all eight operation/fixture combinations. Cbor/MessagePack ratios of the reported means range from about 1.56 to 2.18 for decoding and 1.84 to 2.62 for encoding. Integer-array and string-map decoding allocations match; MessagePack's model decoding allocates 40 B less for one order and 2,080 B less for 64 orders. Both use generated formatters with warmed dependencies, so repeated member resolution is not an explanation established by this comparison. Profiling and disassembly are required to attribute the gaps.

Cbor is faster than the System.Formats adapters and PeterO in every measured combination. REDox encodes the integer array faster (2.194 versus 2.702 µs) and decodes the string map faster (39.123 versus 41.143 µs). String-map encode intervals overlap; these results do not establish Cbor superiority there. Cbor's nested-model operations are faster than REDox's on these fixtures. Wire-schema and policy differences remain relevant controls, not proven causes of the timing differences.

BenchmarkDotNet flags large-model Cbor encoding as bimodal (mValue 3.75). Its retained launch means are 14.603 µs (7 samples) and 13.248 µs (8 samples), with unchanged 8,896 B allocations. The combined 13.881 µs mean does not characterize one stable performance regime. Preserve both launches; investigate code generation and runtime behavior with matched disassembly and additional default-runtime controls before assessing an optimization. No cause has been established.

The earlier scalar-encoding bimodality remains unresolved: this suite contains no scalar control. Other open comparison coverage includes cold initialization and retention, malformed/adverse input, floating-point/tagged built-ins, .NET Standard and .NET 8/9 tiers, segmented buffers, Native AOT performance, other CPUs, and Linux. No hosted CI was run. Local correctness evidence remains 2,241 passing tests, package consumers on .NET 8/9/10, and project/package Native AOT execution on Windows; it does not replace this missing performance coverage.

## Retained evidence

[Summary CSVs, allocation measurements, and both launches](serializer-comparison-data/README.md) are versioned without absolute machine paths. Full local BenchmarkDotNet reports and logs remain in the ignored artifacts directory. The recorded source baseline predates this documentation-only results commit; the measured implementation is unchanged.
