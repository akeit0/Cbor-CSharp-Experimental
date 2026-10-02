# Cbor.SourceGenerator

An incremental generator for explicit CborObject contracts rooted by CborResolver. It generates typed object maps, constructor binding, enum mappings, and closed array/list/dictionary/nullable formatters. Generated dispatch uses closed type references and is Native AOT compatible.

Roslyn dependencies remain private and the generator targets netstandard2.0 with a Roslyn 4.3.1 API floor. Actual SDK consumer compilations exercise the generated compatibility and ref-struct-buffer tiers. Compiler-driver tests check CBOR001/CBOR002, recursive graphs, and edited contracts. Older compiler/Unity hosts have not been verified.

Attributes live in Cbor. The analyzer assembly does not reference the runtime. Closed generic models and opted-in inheritance use substituted symbols, flattened map keys, override checks, and constructor binding. Open or expanding generic graphs and conflicting inherited contracts are diagnosed without partial output; see the typed serializer design for construction limits. External custom formatter annotations remain incomplete. IDE fixes remain a separate project.
