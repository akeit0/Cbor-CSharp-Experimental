# Cbor.SourceGenerator

An incremental generator for explicit CborObject contracts rooted by CborResolver. It generates typed object maps, constructor binding, enum mappings, and closed array/list/dictionary/nullable formatters. Generated dispatch uses closed type references and is Native AOT compatible.

Roslyn dependencies remain private and the generator targets netstandard2.0 with a Roslyn 4.3.1 API floor. Actual SDK consumer compilations exercise the generated compatibility and ref-struct-buffer tiers. Compiler-driver tests check CBOR001/CBOR002, recursive graphs, and edited contracts. Older compiler/Unity hosts have not been verified.

Attributes live in Cbor. The analyzer assembly does not reference the runtime. Generic/inherited models and external custom formatter annotations still need dedicated designs; unsupported shapes are diagnosed. IDE fixes remain a separate project.
