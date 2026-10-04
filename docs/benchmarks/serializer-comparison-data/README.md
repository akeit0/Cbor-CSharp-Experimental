# Serializer comparison data

Measured optimized source: b967c6a0b8ea7ac9d2e19bd36326e8c7089237e1 (runtime implementation 5c0a160172d4c9fadd0dce366363fe8d8b34fb1f). Methodology, dependency pins, machine/runtime settings, payload sizes, results, and limitations are in [the measurement record](../serializer-comparison.md). The [original baseline data](../serializer-comparison-baseline-data/README.md) remain archived separately.

* [Integer array summary](IntegerArrayComparison-report.csv)
* [String map summary](StringMapComparison-report.csv)
* [Nested model summary](NestedModelComparison-report.csv)
* [Both-launch measurements](measurements.csv)
* [Managed allocations and GC counts](gc.csv)

Summary CSVs are BenchmarkDotNet 0.15.8 exports. Allocation units use 1 KB = 1,024 B. Time and allocation ratios use Cbor within each operation/fixture.

The three summaries above retain the complete initial 40-case refresh unchanged, including cases with short-iteration warnings. The main report uses longer targeted confirmation for all five string-map decodes and PeterO model decodes at both sizes. Its [string-map summary](decode-confirmation/StringMapComparison-report.csv), [model summary](decode-confirmation/NestedModelComparison-report.csv), [measurements](decode-confirmation/measurements.csv), and [GC records](decode-confirmation/gc.csv) retain seven additional cases: two launches, five warmups, eight measurements, 500 ms iteration targets. This adds 112 raw workload samples and 14 successful workers. For model confirmation, PeterO is the only selected provider, so those CSV ratio fields are not Cbor ratios; the main report computes all time ratios from selected provider/Cbor means. Original BenchmarkDotNet ratio distributions remain in each export.

measurements.csv transcribes the console's total nanoseconds and operation counts, preserving launch and iteration identifiers. ns_per_operation is total_ns/operations. WorkloadActual retains all 12 measured samples per case before overhead subtraction or outlier filtering: 40 cases × 2 launches × 6 measurements = 480 rows. Each launch uses four warmups and 200 ms iteration targets. OverheadActual records the measured invocation overhead. WorkloadResult contains overhead-adjusted, retained samples used by BenchmarkDotNet; removed outliers can therefore reduce its sample count. The small timestamp rounding in the original console is retained.

gc.csv transcribes each of the 80 successful workers' GC diagnostic lines. The final column divides reported managed allocation bytes by operations. All workers verified loaded runtime assets and complete fixture results before timing. These measurements do not report native allocations, retained resolver/converter state, or pool retention. No local filesystem paths, process IDs, or credentials are included.
