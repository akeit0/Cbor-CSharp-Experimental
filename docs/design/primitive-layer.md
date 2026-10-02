# Primitive and structural validation layer

The primitive API establishes CBOR wire behavior used by CborSerializer, its formatters, and generated model contracts. The typed architecture and application-level behavior are specified separately in [the typed serializer design](typed-serializer.md).

## Responsibilities and ownership

CborPrimitives operates on spans without allocation. Headers preserve their original width and additional-information field. Try-read methods report zero consumption on failure. Try-write methods check capacity before mutating the destination; span byte/text writes support overlap by copying the payload before publishing the header.

Buffer extensions take Foundation buffers by ref. The caller owns and disposes them. Scalars and headers require at most nine contiguous bytes. Byte payload materialization copies directly to the final array. Text decoding uses a borrowed Foundation window and decodes into the final string without a temporary managed byte array. UTF-16 writing validates first and transcodes directly into the destination.

The buffer writer follows the reference and unaligned store pattern in MessagePack v4. It writes through Foundation's borrowed references, uses native unaligned stores with explicit big-endian conversion, and advances only the encoded length. Extended integer headers use a significant-bit lookup table and an eight-byte payload store when a nine-byte window is available; smaller windows use an exact-width cold path. Small integers and simple tokens use a byte store. Fixed destinations retain the actual-size requirement. Preferred floats select their width once and write directly without temporary stack encoding/copying. Public span TryWrite entry points validate capacity before entering the shared exact-width store routines and never perform padded stores.

Named CBOR constants distinguish initial-byte codes, additional-information markers, masks, argument offsets, and encoded widths. Unsafe helpers document their required destination window; no borrowed reference survives another request to the buffer or nested formatter call.

Modern constraints allow ref structs; compatibility assets use Compatible buffers. The small unsafe netstandard2.0 regions pin borrowed span/pointer memory for its entire use. No owned buffer is boxed, captured, or copied into a wrapper.

CborValidation.TryValidate checks exactly one item and rejects trailing data. TryReadValueLength validates a prefix and reports its encoded length, leaving following items to the caller. Failed prefix validation returns zero length and leaves the caller's source untouched. Buffer TrySkipValue can consume a prefix before a later failure and reports that consumption; it cannot resume the failed traversal. Streaming callers should retain their source position until prefix validation succeeds.

## Grammar and resource model

The scanner traverses an explicit frame stack instead of recursing. Containers and tags, including empty containers, each count as a nesting level; a root scalar is level zero. Default limits are 64 levels, one million tokens, and 16 MiB encoded bytes per item. Depths above 64 rent a bounded frame array returned in finally; the configurable ceiling is 1024.

Each tag, scalar, container, or definite string chunk charges the item budget. Break markers charge bytes. Declared payload lengths are checked before traversal. Array/map child counts are bounded before iteration; map pair counts are bounded before multiplication. No wire length controls a proportional allocation during validation.

Break cannot close a definite container or tag. An indefinite map must finish in a key position. Indefinite strings accept only definite chunks of the same type. UTF-8 state crosses buffer seams but resets per definite text chunk. Overlong sequences, surrogates, out-of-range scalars, stray continuations, and incomplete sequences are rejected.

Resource failures return LimitExceeded. Forbidden indefinite encodings or non-preferred widths return EncodingPolicyViolation, distinct from malformed syntax. Scalar/header errors never consume input; text materialization can consume its header before rejecting invalid UTF-8.

## Integer and float representation

Unsigned values and negative arguments retain the complete UInt64 range. A negative argument means -1 minus argument, down to -2^64. Int64 decoding rejects values outside the CLR range instead of wrapping. Bignum tag contents await typed semantic formatters.

All three float widths decode into Double. Raw half bits and explicit single/double writes support width and NaN-payload preservation. Preferred Double writing chooses the shortest exact width, preserves negative zero, and normalizes NaN to f97e00.

All 65,536 half bit patterns are checked against runtime Half conversion. Seeded double/single cases are compared to the independent BCL canonical writer. RequirePreferredEncoding checks shortest numeric/length/tag widths and the documented canonical NaN policy. It does not by itself prohibit indefinite containers; AllowIndefiniteLength is separate.

## Semantic boundaries

Structural acceptance does not establish registered-tag validity, duplicate/equivalent map-key handling, deterministic map order, or application schemas. Low-level reads/writes do not track container balance. Low-level materializing reads handle definite strings. Typed string/byte-array formatters also aggregate indefinite chunks under shared context limits.

Typed formatters supply container/context state, per-operation limits, required/unknown members, and specified CLR key semantics. Broad tag mappings and deterministic key-equivalence profiles remain incomplete. Async streaming must keep the synchronous core and define cancellation and item boundaries in the outer layer.

## Evidence

Offline fixtures contain all 81 RFC Appendix A encodings and all listed Appendix F malformed inputs. Tests check every seam, one-byte/empty segments, and every truncation prefix across all four library assets.

The independent oracle uses BCL Lax to avoid imposing duplicate-key semantics, then validates text and simple-value items under Strict separately: Lax tolerates invalid UTF-8 and reserved extended simple values. These differences have regression fixtures. Generated nested items/mutations and random bytes supplement the specified wire corpus.

Native AOT runs the valid/malformed corpus, segmented reads, generated models, dictionaries, and 1024-level traversal. Freshly packed artifacts are installed into independent .NET 8/9/10 consumers that compile and run generated models; a deliberately invalid buffer copy must fail with transitive SF002. The generator is functional. The separate code-fix assembly is still empty.

Integer benchmarks use matching encoded inputs and compare lifecycle allocations with System.Formats.Cbor. Preliminary microbenchmarks do not establish whole-object performance or MessagePack v4 parity.
