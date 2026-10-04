# Typed hot-path optimization — 2026-10-03

The implementation follows the pinned MessagePack v4 strategy of struct element codecs and batched buffer access, while preserving CBOR wire semantics, resource policies, and custom overrides. Public serializer/formatter APIs are unchanged.

* The deserialization prefix guard keeps complete, valid extended arguments on a direct byte/length check. Additional information 24..27 selects 1, 2, 4 or 8 payload bytes; a shift computes this size without a multiway length dispatch. Extended simple values, reserved/indefinite forms, truncated/segmented prefixes and preferred-width policy checks retain their full validation paths.
* Built-in integer array writers opt into an internal bulk interface during formatter initialization. Struct codecs cover the eight CLR integer types, reuse available windows, and amortize GetSpan/Advance calls. Item checks precede storage acquisition, byte checks still follow every token, and pending bytes publish in finally on failure. Tight windows use the resolved scalar formatter. Scalar overrides retain their per-item calls. Default byte arrays retain their byte-string mapping.
* Definite lists iterate their prevalidated count directly. Indefinite lists retain per-element growth limits and break handling. The definite header already bounds collection, declared allocation, remaining input and item counts.

References: MessagePack v4's [primitive collection formatters](https://github.com/MessagePack-CSharp/MessagePack-CSharp/blob/a9057be29b0621d56a4312f79172e759bbb4e1de/src/MessagePack/Formatters/PrimitiveCollectionFormatters.cs), [struct codecs and batched element runs](https://github.com/MessagePack-CSharp/MessagePack-CSharp/blob/a9057be29b0621d56a4312f79172e759bbb4e1de/src/MessagePack/Formatters/PrimitiveCollectionFormatters.ElementCodecs.cs), and [generic collection loops](https://github.com/MessagePack-CSharp/MessagePack-CSharp/blob/a9057be29b0621d56a4312f79172e759bbb4e1de/src/MessagePack/Formatters/GenericCollectionFormatters.cs). The CBOR implementation is original code; upstream sources remain unmodified.

## Development and confirmation

Development runs selected eight Cbor-only operations or six small-array controls, with one launch, three warmups, three measurements and 150 ms iterations. Each took approximately 30 seconds. Focused decode rechecks used three cases and six measurements. The original 40-case, long five-library suite was not rerun during development.

A promising candidate received broad verification before stronger timing confirmation. Baseline source is [dd23fb8](https://github.com/akeit0/Cbor-CSharp-Experimental/tree/dd23fb8ca178779775be2c91e0f541e0674531ab), extracted into an isolated ignored snapshot. Final implementation is [5c0a160](https://github.com/akeit0/Cbor-CSharp-Experimental/tree/5c0a160172d4c9fadd0dce366363fe8d8b34fb1f). The exact same development fixture was added to that snapshot; runtime sources are untouched. Both builds use the same unmodified pinned MessagePack submodule and locked dependencies. The baseline and final workers each check runtime assets and complete fixture results before timing.

The final confirmation uses 14 Cbor operations/controls, two launches, four warmups, six measurements per launch and 200 ms iterations, run sequentially with no simultaneous builds or tests. Host: Windows 11 x64, Intel Core i7-13700F (16 physical / 24 logical cores), SDK 11.0.100-rc.1.26425.128, .NET 10.0.12 x64 RyuJIT, concurrent workstation GC. Tables report means and 99.9% interval half-widths; managed allocations are unchanged unless stated. Results are warmed operations, excluding initialization/retained graph state.

```powershell
# From the comparison directory, after verifying/building each source version:
dotnet run -c Release --no-build --no-restore -- --filter '*Comparison.Cbor*' '*IntegerArrayDevelopment*' --job short --warmupCount 4 --iterationCount 6 --launchCount 2 --iterationTime 200 --artifacts ../../artifacts/benchmarks/optimization-confirm-final
```

## Confirmed measurements

The eight existing Cbor comparison operations use the original fixtures. Times below are microseconds; allocations are exact managed bytes per operation from GC records and are identical before/after.

| Fixture | Operation | Before, mean ± error (µs) | After, mean ± error (µs) | Allocated bytes |
| --- | --- | ---: | ---: | ---: |
| Integer array, 1,024 | Decode | 5.672 ± 0.3421 | 3.366 ± 0.0382 | 4,120 |
| Integer array, 1,024 | Encode | 2.664 ± 0.0370 | 1.976 ± 0.0183 | 3,032 |
| String map, 1,024 | Decode | 39.86 ± 0.617 | 38.55 ± 0.477 | 72,168 |
| String map, 1,024 | Encode | 14.37 ± 0.132 | 14.53 ± 0.144 | 15,024 |
| Nested model, 1 order / 8 lines | Decode | 0.3292 ± 0.00231 | 0.3198 ± 0.00425 | 888 |
| Nested model, 1 order / 8 lines | Encode | 0.2273 ± 0.00139 | 0.2247 ± 0.01436 | 168 |
| Nested model, 64 orders / 512 lines | Decode | 19.7505 ± 0.32776 | 19.3917 ± 0.46729 | 49,272 |
| Nested model, 64 orders / 512 lines | Encode | 13.2096 ± 0.11645 | 13.8187 ± 0.19949 | 8,896 |

The added controls use prefixes of the same integer-array fixture. Times below are nanoseconds. The one-element writer uses the scalar path; the other counts exercise bulk writing and the complete-prefix decode check.

| Count | Operation | Before, mean ± error (ns) | After, mean ± error (ns) | Allocated bytes |
| ---: | --- | ---: | ---: | ---: |
| 1 | Encode | 16.890 ± 2.0415 | 15.399 ± 0.5158 | 32 |
| 1 | Decode | 9.773 ± 0.2068 | 9.864 ± 0.1004 | 32 |
| 8 | Encode | 30.309 ± 0.5778 | 30.239 ± 3.2416 | 48 |
| 8 | Decode | 54.190 ± 1.0596 | 32.623 ± 1.3087 | 56 |
| 1,024 | Encode | 2,682.485 ± 63.6823 | 2,015.713 ± 28.8371 | 3,032 |
| 1,024 | Decode | 5,756.114 ± 277.5579 | 3,399.496 ± 44.4110 | 4,120 |

The original integer-array case has approximately 40.7% lower decode time and 25.8% lower encode time. The independent control reproduces the large-array gains. One-element decode and one/eight-element encode intervals overlap; these runs do not establish changes there. Map encode and large-model decode intervals also overlap.

Large-model encode has a 4.6% higher mean with separated intervals in this confirmation. This remains an open regression concern requiring matched code-generation/runtime controls and profiling; its cause is not established. Earlier measurements also showed launch-dependent encoding distributions. Absence of a multimodality warning in this run does not resolve those observations. The optimization is not a uniform speedup across workloads.

[Versioned summaries, both launches, raw workload/overhead samples and GC records](hot-path-data/README.md) retain the evidence. These are Cbor-only before/after measurements; they do not refresh the historical [five-library results](serializer-comparison-baseline.md) or establish MessagePack performance parity. A subsequent [main comparison refresh](serializer-comparison.md) measures all five providers against this optimized implementation separately. Cold graph creation/retention, other runtime/buffer tiers, floats/bools/lists and additional hosts need separate measurements before extending the integer-array optimization.

## Correctness and code generation

Broad verification passes 2,406 tests across netstandard2.0/2.1 and .NET 8/9/10 assets, with zero build warnings/errors. Fresh packaged .NET 8/9/10 consumers and SF002/CBOR003 enforcement pass. Project and fresh packaged Windows Native AOT consumers exercise generated signed/unsigned integer arrays, RFC boundary tokens and batches of 1,025 elements.

New checks cover complete prefixes in every major type, fragmented prefixes, malformed/truncated forms and preferred-width rejection before custom formatter calls. Array tests cover all integer types, fixed RFC token expectations, exact guarded writer windows, batch/window boundaries, custom integer overrides, and item/encoded-budget failure prefixes. A caller-defined writer rejects any element-storage acquisition after the item budget is exhausted.

Focused Int32 encoding disassembly on .NET 10 shows guarded devirtualization and an inlined struct codec in the batch loop. The loop emits sign/width selection and stores directly, retaining item and encoded-byte checks; no per-element formatter/codec interface dispatch appears in that observed bulk path. Scalar/tight-window fallback remains. This is evidence for the observed Int32 specialization, not every runtime/platform or cold path.
