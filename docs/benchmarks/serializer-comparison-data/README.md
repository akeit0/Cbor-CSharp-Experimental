# Serializer comparison data

Measured source: f2fd74a4e263e2776129bdef3206e19eafa402a5. Methodology, dependency pins, machine/runtime settings, payload sizes, results, and limitations are in [the measurement record](../serializer-comparison.md).

* [Integer array summary](IntegerArrayComparison-report.csv)
* [String map summary](StringMapComparison-report.csv)
* [Nested model summary](NestedModelComparison-report.csv)
* [Both-launch measurements](measurements.csv)
* [Managed allocations and GC counts](gc.csv)

Summary CSVs are BenchmarkDotNet 0.15.8 exports. Allocation units use 1 KB = 1,024 B. Time and allocation ratios use Cbor within each operation/fixture.

measurements.csv transcribes the console's total nanoseconds and operation counts, preserving launch and iteration identifiers. ns_per_operation is total_ns/operations. WorkloadActual retains all 16 measured samples per case before overhead subtraction or outlier filtering. OverheadActual records the measured invocation overhead. WorkloadResult contains overhead-adjusted, retained samples used by BenchmarkDotNet; removed outliers can therefore reduce its sample count. The small timestamp rounding in the original console is retained.

gc.csv transcribes each worker's GC diagnostic line. The final column divides reported managed allocation bytes by operations. These measurements do not report native allocations, retained resolver/converter state, or pool retention. No local filesystem paths, process IDs, or credentials are included.
