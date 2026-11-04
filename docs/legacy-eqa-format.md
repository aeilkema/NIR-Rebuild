# NIRSystem 5000 type-9 EQA format

This is a compatibility reader derived from the authorized corpus and
read-only Ghidra analysis of `predictionrelease.dll`. It is not a claim that
all historical EQA variants share this layout.

## Proven layout

- File header: `0x180` bytes.
- Repeated record size: `0x5F8` bytes.
- Record name: offset `0x00`, fixed 16-byte ASCII field.
- Term count: offset `0x12`, signed 16-bit little-endian.
- Degrees of freedom: offset `0x14`, signed 16-bit little-endian.
- Preprocessing metadata: offset `0x1A` (`bias`), `0x1E` (`mean`),
  `0x22` (`SEC`), `0x26` (`R²`).
- Preprocessing header: record offset `0x80`, four little-endian 16-bit
  values. The fixed-record corpus consistently contains `1,4,4,1`.
- Selection mask: record offset `0x88`, one byte per source point.
- Coefficients: immediately after the source-point mask, one little-endian
  IEEE-754 float per term.

The native prediction routine applies the preprocessing transformation,
selects non-zero mask positions, computes a coefficient dot product and adds
the record bias. The implementation retains the preprocessing boundary as a
separate service so each step can be regression-tested.

Unknown/variant EQA layouts are inspected but are not forced through this
reader.
