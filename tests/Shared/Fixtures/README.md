# Wire fixtures

rfc8949-appendix-a.txt contains all 81 hexadecimal encodings in RFC 8949 Appendix A, Table 6, including alternative float widths and indefinite-length items. Hex strings were extracted from the official plain-text RFC on 2026-10-02, retaining wrapped continuations. These are factual encoded data examples, not copied implementation code.

Source: https://www.rfc-editor.org/rfc/rfc8949.txt
HTML: https://www.rfc-editor.org/rfc/rfc8949.html#appendix-A

rfc8949-appendix-f-invalid.txt contains the malformed encodings listed in Appendix F.1, one per line. Tests separately assert syntax-error classifications and resource limits; some incomplete huge-length encodings are rejected by resource limits before truncation is established.

Fixtures are embedded into test executables and never fetched during build or test. Keep the vector-count assertion when updating these files.

## SipHash-2-4 fixtures

siphash24.txt contains the 64 eight-byte outputs from vectors_sip64 in the authors' CC0 reference [vectors.h](https://github.com/veorq/SipHash/blob/master/vectors.h), retrieved 2026-10-02. Lines use little-endian byte order. The key is bytes 00 through 0F; message n is bytes 00 through n-1, for n=0..63.
