# Serializer comparison

One executable compares Cbor, MessagePack-CSharp v4, CAPCOM REDox.Cbor, System.Formats.Cbor, and PeterO.Cbor on .NET 10. All encoding methods return a new owned byte array; all decoding methods return complete CLR values. Model/converter initialization, fixture validation, and payload-size reporting happen in GlobalSetup.

## Dependencies and build

MessagePack v4 is not on NuGet. `external/MessagePack-CSharp` is a submodule pinned to a9057be29b0621d56a4312f79172e759bbb4e1de from the v4 branch. Its original project, generated formatters, and dependency declarations are used without modifying upstream sources. The comparison's global.json matches upstream's .NET 11 preview SDK requirement; measured workers target and execute .NET 10. The core solution retains its .NET 10 SDK. Analysis is pinned to .NET 10 rules so changing the comparison compiler does not silently change the core's style checks.

REDox.Cbor uses NuGet `CAPCOM.REDox.Cbor` 1.0.0; PeterO uses `PeterO.Cbor` 4.5.5; System.Formats.Cbor uses 10.0.12. Versions and transitive dependencies are locked. Upstream submodule dependency versions remain upstream-owned. Runtime-asset checks reject an incorrect framework tier or a MessagePack model formatter from outside this assembly.

PeterO's reference uses `extern alias PeterOCbor`. This project's runtime assembly is explicitly named `Cbor.CSharp`, avoiding a runtime-loader collision with PeterO's `CBOR`; namespace `Cbor` is retained. Every provider runs in the same executable.

From the repository root, initialize and verify:

```powershell
git submodule update --init external/MessagePack-CSharp
pwsh -NoProfile -File eng/verify-comparison.ps1
```

Install the SDK in this project's global.json and the .NET 10 runtime first. Run benchmarks from this directory so SDK selection uses that file:

```powershell
Set-Location benchmarks/Cbor.Benchmarks.Comparison
dotnet run -c Release --no-build --no-restore -- --filter '*Comparison*' --job short --warmupCount 5 --iterationCount 8 --launchCount 2 --iterationTime 500 --artifacts ../../artifacts/benchmarks/serializer-comparison
```

Complete builds/tests before measurements. Run suites sequentially. Preserve the complete reports, including errors, allocations, and warnings. Hosted CI remains disabled; this comparison is not part of the core verification solution's build graph.

## Contracts and controls

* Integer arrays contain 1,024 signed values spanning positive/negative compact widths and both Int32 endpoints.
* String-key maps contain 1,024 UTF-8 keys and signed integer values.
* Nested models contain one or 64 orders with eight lines each, UTF-8 customer names, integer prices, and mutable lists. Cbor and MessagePack use generated paired formatters with warmed child dependencies. MessagePack uses DefaultAot and rejects legacy formatter fallback; MaxDepth is 64. REDox and PeterO use their native default typed conversion APIs.

CBOR collection payloads are cross-read by every CBOR provider. Every decoded element, key/value, order, and line is checked. System.Formats.Cbor independently reads all CBOR model payloads. Its schema-specific adapters emit the same definite integer maps as Cbor, and byte equality is asserted for their array/map/model outputs. These adapters are benchmark reference code, not a general serializer or a substitute for the core's resource policies.

System.Formats.Cbor has no CLR object serializer; the adapters include reader/writer construction and typed materialization in measurement. PeterO's `FromObject`/`ToObject` conversion and DOM construction are timed, rather than timing a prebuilt DOM. REDox's DOM/converter work is inside its native serializer calls. Warm caches/pools are used according to each library's normal APIs.

Model wire formats differ: Cbor and the System.Formats adapter use integer-keyed CBOR maps; MessagePack uses indexed arrays; REDox uses property-name CBOR maps; PeterO uses camel-cased property-name CBOR maps. Payload sizes are recorded separately. Defaults differ in safety checks, limits, and encoding choices; the fixtures are valid bounded inputs, and results do not establish equivalent error behavior or security guarantees. Ratios compare complete operations on equivalent CLR data, not identical wire work or full feature parity.
