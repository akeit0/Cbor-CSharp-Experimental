# Typed serializer architecture

The product is a CBOR serializer. Primitive parsing and structural validation support typed decoding, streaming boundaries, and version-unknown data. They do not replace object serialization.

## Formatter and resolver contract

ICborFormatter<T> converts one CLR value to/from one CBOR item. Its methods are generic over borrowed Foundation buffers, passed by reference. .NET 9+ permits ref-struct buffers; compatibility assets use Foundation's Compatible buffers. Formatters neither own nor dispose buffers and must be stateless/thread-safe.

A formatter instance works with every supported buffer tier. Nested values go through CborSerializationContext or CborDeserializationContext, which carry the operation's options and budgets. Nullable and enum wrappers delegate directly to the underlying formatter because they represent the same wire item, not another child. Containers pair EnterContainer and ExitContainer in a finally block. Custom formatters must follow these contracts.

CborBuiltinResolver uses a generic static cache for scalar/string formatters. CborFormatterRegistry builds immutable snapshots of explicit typed registrations. CborCompositeResolver resolves in order; per-operation options preserve that selection throughout the graph. There is no global mutable registration table or reflection fallback.

Generated resolvers cache closed formatter instances per requested type. Their explicit root list closes model, enum, nullable, array, list, and dictionary dependencies at compile time, including recursive graphs. Formatters acquire children through the operation's resolver, so custom overrides continue to apply at every nesting level. Stateless formatters avoid recursive initialization and shared mutable construction graphs.

Collection operations resolve child formatters once before their element loop. Generated object methods use typed local slots, resolved lazily once per distinct CLR member type within that object call. Absent members and null objects do not resolve unused children. Slots belong to the call, so recursive types avoid initialization cycles and concurrent operations retain their own resolver overrides. Context overloads accepting a resolved formatter retain the same item, depth, encoding-policy, and byte checks. Generated contract keys are structural integers and use dedicated context operations, so a custom integer formatter affects member values without changing schema identities.

## Wire contracts

| CLR shape | CBOR representation |
| --- | --- |
| Boolean | Simple false/true |
| Integral types | Integer; checked narrowing on read |
| Single/Double | Shortest exact float width; canonical half NaN; negative zero preserved |
| String | Strict UTF-8 text; null remains null |
| Byte array | Byte string; null remains null |
| Nullable value | Underlying wire item or null |
| Enum | Declared integral underlying type; unknown enum values preserved |
| Array/List | Array, or null |
| Dictionary | Map, or null |
| CborObject class/struct | Map with explicit nonnegative integer member keys |

Single reads may round an in-range double to Single precision, but reject finite values outside Single's finite range. Float formatters do not reinterpret integer or arbitrary tagged items as floats. Undefined is not a synonym for null. Tagged CLR built-ins still need separately specified mappings.

Writers emit definite lengths. Readers accept definite and indefinite strings/collections unless policy forbids them. Every text chunk must individually be valid UTF-8. Aggregation bounds the combined byte length before growing pooled storage; the pool is returned in finally and cleared. A constrained struct materializer specializes the shared chunk traversal for string versus byte-array results without object casts or an interface-typed strategy instance. This works on the .NET Standard tier as well as modern .NET.

Generated object writes sort member keys by integer value. This does not establish deterministic encoding for arbitrary nested dictionaries. Missing optional members receive default(T); Required means presence, not a non-null value requirement. Known duplicate keys fail. Unknown keys and values are traversed with the same policies and budgets, including malformed nested data. Unknown-key duplicates are not interpreted as object members.

All public instance members must explicitly opt in with CborKey or opt out with CborIgnore. Readonly members bind to accessible constructor parameters by name and CLR type; CborConstructor can select a constructor. Construction occurs after all entries and required-member checks pass. Keys are stable schema identities and must never be reassigned after removal.

Constructor-bound members are not assigned again in the object initializer. C# required members must be initialized by that initializer or covered by a constructor marked SetsRequiredMembers; compiler requirements and CborKey.Required wire-presence requirements are separate contracts.

## Resource and ownership behavior

Serializer reads process the input once, rather than validating an entire message and parsing it again. A bounded input length is checked before decoding. Declared collection counts share a message-wide child budget and cannot force allocations inconsistent with the available bytes or configured limits. Actual items, including unknown-member tokens and indefinite string chunks, share an item budget. Containers share a depth budget.

Typed reads inspect the initial byte and use specialized scalar/length decoders. Valid contiguous extended integers remain on that path; seams and invalid input enter cold helpers. Default context checks validate the token prefix without reconstructing a general header or decoding its argument again. Preferred encoding remains an explicit policy with numeric and floating-point checks. Unknown structural traversal uses the general decoder, with value-type scan limits and a scalar path that avoids allocating a traversal stack.

Text serialization computes strict UTF-8 byte length once before encoding. Built-in string and byte-array writers preflight the complete header plus payload against remaining encoded bytes before requesting output memory. Custom formatters can use CheckEncodedLength with the buffer's absolute position and planned bytes; the post-formatter byte check remains a backstop rather than rollback.

Dictionary deserialization rejects duplicate CLR keys and null keys. Built-in comparers use a per-process CSPRNG key and SipHash-2-4, avoiding predictable integer/string hash collision families across buffer tiers. Floating-point comparers normalize signed zeros and NaN representations consistently with CLR equality. Other key types require an explicit comparer; its equality and collision resistance are the caller's contract. SipHash is independently checked against all 64 author vectors.

Modern dictionary readers reserve an entry with one lookup, rejecting a duplicate before decoding its value. The dictionary remains private until successful return, so nested formatters cannot resize its entry storage while a managed value reference is in use. A failed value decode abandons the dictionary and context. Compatibility assets retain ContainsKey/Add because their reference assemblies expose no safe entry-reference insertion API.

Structural UTF-8 checks use the runtime ASCII validator on modern assets. Non-ASCII text uses bounded unaligned four-byte loads to check whole code points, including overlong, surrogate, and Unicode upper-bound restrictions; byte state is limited to tails and buffer seams. Compatibility assets use the same whole-code-point checks and four-byte ASCII skips. Loads stay within the supplied span, and endianness is explicit. State may cross buffer segments but never text chunk boundaries.

Public entry points own their Foundation buffer wrappers and dispose them in finally. Caller-owned writers remain open. Borrowed-buffer overloads do not dispose or flush. Deserialize requires exactly one item and rejects trailing bytes. Streaming writes can commit a prefix before failure; rollback belongs to the caller. A context is for one operation and must be abandoned after a formatter failure.

## Evidence and remaining work

Generated contracts compile and run against netstandard2.0, netstandard2.1, net9.0, and net10.0 assets. Tests cover fixed bytes, every sequence seam, one-byte/empty segmentation, truncation, mutable/immutable objects, records, structs, enums, recursive graphs, nested collections, required/duplicate/unknown members, shared budgets, concurrent immutable resolver use, and an independent System.Formats.Cbor object oracle. Compiler-driver tests exercise invalid contracts and edited generation inputs. The packed NuGet consumer uses generated mutable/immutable models; Windows Native AOT executes generated models, segmented data, and keyed dictionaries.

The initial generated model surface excludes generic/inherited models, unions, circular-reference preservation, and external formatter annotations. Broad semantic-tag built-ins, deterministic dictionary ordering/key-equivalence profiles, async outer streaming/cancellation, IDE fixes, shrinking/coverage-guided fuzzing, and comparable object benchmarks remain required for full serializer maturity. The project does not claim released MessagePack parity from these tests.
